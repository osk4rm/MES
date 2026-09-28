using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Confirmation-time skill qualification gating (issue #397): an operator
/// reporting a confirmation must hold every skill the order's recipe
/// operations require. Requirements that resolve to no skill row are legacy
/// free text and never block.
/// </summary>
public class CreateProductionConfirmationSkillGatingTests
{
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();
    private readonly Mock<IStockMovementsRepository> _movements = new();
    private readonly Mock<ILotsRepository> _lots = new();
    private readonly Mock<ILotGenealogyEdgesRepository> _edges = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMaterialReservationsRepository> _reservations = new();
    private readonly Mock<IOperationNodesRepository> _operationNodes = new();
    private readonly Mock<ISkillsRepository> _skills = new();
    private readonly Mock<IOperatorSkillQualificationsRepository> _qualifications = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _operatorId = Guid.NewGuid();

    public CreateProductionConfirmationSkillGatingTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        _reservations.Setup(r => r.ListForOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation>());
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
        _operationNodes.Setup(r => r.ListForVersionsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperationNode>());
        _skills.Setup(r => r.ListByCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Skill>());
        _qualifications.Setup(r => r.ListSkillCodesForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
    }

    private CreateProductionConfirmationRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _children.Object, _movements.Object, _lots.Object,
            _edges.Object, _guids.Object, _clock.Object, _tenant.Object, _uow.Object, _reservations.Object,
            _operationNodes.Object, _skills.Object, _qualifications.Object);

    private static ProductionOrder ReleasedOrder() => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = ProductionOrderStatus.Released,
        ReleasedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private CreateProductionConfirmationRequest ValidRequest(Guid orderId, Guid? operatorId) => new(
        orderId,
        Guid.NewGuid(),
        operatorId,
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        10m,
        2m,
        null);

    private void ArrangeSkillGate(
        ProductionOrder order,
        string requiredCapability,
        string? requiredRole = null,
        bool skillExists = true,
        IReadOnlyCollection<string>? heldCodes = null)
    {
        var operation = new OperationNode
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            RecipeVersionId = order.RecipeVersionId,
            Code = "OP-10",
            Name = "Welding",
            ResourceRequirements = new List<ResourceRequirement>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    OperationNodeId = Guid.NewGuid(),
                    RequiredCapability = requiredCapability,
                    RequiredRole = requiredRole
                }
            }
        };
        _operationNodes.Setup(r => r.ListForVersionsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperationNode> { operation });

        var code = OperatorSkillGating.ExtractCapabilityCode(requiredCapability)
            ?? requiredRole?.Trim();
        // Every candidate resolves to a skill row unless the scenario models
        // legacy free text (skillExists: false).
        _skills.Setup(r => r.ListByCodesAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> codes, CancellationToken _) => skillExists
                ? codes.Select(c => new Skill { Id = Guid.NewGuid(), TenantId = _tenantId, Code = c, Name = "Skill" }).ToList()
                : new List<Skill>());

        _qualifications.Setup(r => r.ListSkillCodesForOperatorAsync(
                _operatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldCodes?.ToList() ?? new List<string>());
    }

    [Fact]
    public async Task Handle_UnqualifiedOperator_ThrowsValidationExceptionNamingMissingSkill()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        ArrangeSkillGate(order, "WELD — Welding");

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id, _operatorId), CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Message.Should().Contain("WELD");
        _confirmations.Verify(
            r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_QualifiedOperator_ConfirmsSuccessfully()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        ArrangeSkillGate(order, "WELD — Welding", heldCodes: ["WELD"]);

        // Act
        var result = await CreateSut().Handle(ValidRequest(order.Id, _operatorId), CancellationToken.None);

        // Assert
        result.ProductionOrderId.Should().Be(order.Id);
        result.ReportedByOperatorId.Should().Be(_operatorId);
        order.Status.Should().Be(ProductionOrderStatus.InProgress);
    }

    [Fact]
    public async Task Handle_NullOperator_SkipsSkillGateEntirely()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        await CreateSut().Handle(ValidRequest(order.Id, null), CancellationToken.None);

        // Assert
        _operationNodes.Verify(
            r => r.ListForVersionsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_LegacyFreeTextCapability_Ignored()
    {
        // Arrange - the capability matches no skill row, so nothing is enforced
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        ArrangeSkillGate(order, "some legacy free text", skillExists: false);

        // Act
        var result = await CreateSut().Handle(ValidRequest(order.Id, _operatorId), CancellationToken.None);

        // Assert
        result.ProductionOrderId.Should().Be(order.Id);
    }

    [Fact]
    public async Task Handle_RequiredRole_EnforcedAsSkillCode()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        ArrangeSkillGate(order, "WELD — Welding", requiredRole: "LEAD", heldCodes: ["WELD"]);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id, _operatorId), CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Message.Should().Contain("LEAD");
    }

    [Theory]
    [InlineData("WELD — Welding", "WELD")]
    [InlineData("WELD", "WELD")]
    [InlineData("  WELD  ", "WELD")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void ExtractCapabilityCode_ParsesDisplayString(string? capability, string? expected)
    {
        // Act
        var code = OperatorSkillGating.ExtractCapabilityCode(capability);

        // Assert
        code.Should().Be(expected);
    }

    [Fact]
    public void ExtractRequiredSkillCodes_CollectsCapabilityAndRole()
    {
        // Arrange
        var requirements = new List<ResourceRequirement>
        {
            new() { Id = Guid.NewGuid(), TenantId = _tenantId, OperationNodeId = Guid.NewGuid(), RequiredCapability = "WELD — Welding", RequiredRole = "LEAD" },
            new() { Id = Guid.NewGuid(), TenantId = _tenantId, OperationNodeId = Guid.NewGuid(), RequiredCapability = "weld — other name" },
            new() { Id = Guid.NewGuid(), TenantId = _tenantId, OperationNodeId = Guid.NewGuid() }
        };

        // Act
        var codes = OperatorSkillGating.ExtractRequiredSkillCodes(requirements);

        // Assert - deduplicated case-insensitively
        codes.Should().BeEquivalentTo("WELD", "LEAD");
    }
}
