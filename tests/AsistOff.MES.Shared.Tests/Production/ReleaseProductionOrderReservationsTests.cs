using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Release;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Release-time reservation coverage (issue #291): releasing an order with
/// BOM items creates one Active reservation per (product, warehouse) pair,
/// orders without BOM items reserve nothing, and already-reserved pairs are
/// skipped so a re-release never duplicates rows.
/// </summary>
public sealed class ReleaseProductionOrderReservationsTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IRecipeVersionsRepository> _versions = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();
    private readonly Mock<IMaterialReservationsRepository> _reservations = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ICurrentUserAccessor> _user = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc);

    public ReleaseProductionOrderReservationsTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _reservations.Setup(r => r.ListForOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation>());
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
    }

    private ReleaseProductionOrderRequestHandler CreateSut() =>
        new(_orders.Object, _versions.Object, _children.Object, _reservations.Object,
            _guids.Object, _clock.Object, _user.Object, _tenant.Object, _uow.Object);

    private static ProductionOrder PlannedOrder(decimal plannedQuantity = 100m) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = plannedQuantity,
        Status = ProductionOrderStatus.Planned
    };

    private static BomItem Item(
        Guid productId, decimal quantity,
        BomQuantityType type = BomQuantityType.PerUnit,
        decimal? scrap = null, Guid? warehouseId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OperationNodeId = Guid.NewGuid(),
        ProductId = productId,
        Quantity = quantity,
        QuantityType = type,
        ScrapPercentage = scrap,
        PreferredWarehouseId = warehouseId
    };

    private void SetupReleasedVersion(ProductionOrder order) =>
        _versions.Setup(v => v.GetAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecipeVersion
            {
                Id = order.RecipeVersionId,
                RecipeId = order.RecipeId,
                VersionNumber = 1,
                Status = RecipeVersionStatus.Released
            });

    [Fact]
    public void Request_ImplementsTenantRequest()
    {
        typeof(ReleaseProductionOrderRequest).Should()
            .Implement<ITenantRequest<ProductionOrderResponse>>();
    }

    [Fact]
    public async Task Handle_WithBomItems_CreatesOneActiveReservationPerPair()
    {
        // Arrange
        var order = PlannedOrder(plannedQuantity: 100m);
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        SetupReleasedVersion(order);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>
            {
                Item(productA, 2m, warehouseId: warehouse),
                Item(productA, 3m, warehouseId: warehouse),
                Item(productB, 5m, BomQuantityType.PerBatch, scrap: 10m)
            });
        IReadOnlyCollection<MaterialReservation>? saved = null;
        _reservations
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<MaterialReservation>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<MaterialReservation>, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateSut().Handle(new ReleaseProductionOrderRequest(order.Id), CancellationToken.None);

        // Assert — (A, warehouse): (2+3)*100 = 500; (B, null): 5 + 10% = 5.5.
        result.Status.Should().Be(ProductionOrderStatus.Released);
        saved.Should().NotBeNull();
        saved.Should().HaveCount(2);
        saved.Should().ContainSingle(x =>
            x.ProductId == productA && x.WarehouseId == warehouse && x.QuantityReserved == 500m);
        saved.Should().ContainSingle(x =>
            x.ProductId == productB && x.WarehouseId == null && x.QuantityReserved == 5.5m);
        saved.Should().OnlyContain(x =>
            x.ProductionOrderId == order.Id
            && x.TenantId == _tenantId
            && x.QuantityRelieved == 0m
            && x.Status == ReservationStatus.Active);
    }

    [Fact]
    public async Task Handle_NoBomItems_AddsNoReservations()
    {
        // Arrange
        var order = PlannedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        SetupReleasedVersion(order);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());

        // Act
        var result = await CreateSut().Handle(new ReleaseProductionOrderRequest(order.Id), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ProductionOrderStatus.Released);
        _reservations.Verify(
            r => r.AddRangeAsync(It.Is<IReadOnlyCollection<MaterialReservation>>(c => c.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyReservedPairs_SkipsDuplicates()
    {
        // Arrange
        var order = PlannedOrder();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        SetupReleasedVersion(order);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>
            {
                Item(productA, 2m),
                Item(productB, 3m)
            });
        _reservations.Setup(r => r.ListForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    ProductionOrderId = order.Id,
                    ProductId = productA,
                    QuantityReserved = 200m,
                    CreatedAt = _now
                }
            });
        IReadOnlyCollection<MaterialReservation>? saved = null;
        _reservations
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<MaterialReservation>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<MaterialReservation>, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().Handle(new ReleaseProductionOrderRequest(order.Id), CancellationToken.None);

        // Assert — only the missing pair is inserted.
        saved.Should().NotBeNull();
        saved.Should().ContainSingle()
            .Which.ProductId.Should().Be(productB);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        // Act
        var act = () => CreateSut().Handle(new ReleaseProductionOrderRequest(id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _reservations.Verify(
            r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<MaterialReservation>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TransactionFails_RestoresOrderStatus()
    {
        // Arrange — the fan-out transaction throws so the rolled-back
        // in-memory Released mutations must be restored on the tracked order.
        var order = PlannedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        SetupReleasedVersion(order);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem> { Item(Guid.NewGuid(), 2m) });
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        // Act
        var act = () => CreateSut().Handle(new ReleaseProductionOrderRequest(order.Id), CancellationToken.None);

        // Assert — the exception propagates and tracked state is restored.
        await act.Should().ThrowAsync<InvalidOperationException>();
        order.Status.Should().Be(ProductionOrderStatus.Planned);
        order.ReleasedAt.Should().BeNull();
        order.ReleasedByUserId.Should().BeNull();
    }
}
