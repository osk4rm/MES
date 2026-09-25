using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations;
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
/// Atomicity tests for the operator confirmation fan-out (issue #265):
/// the confirmation row, RW/PW movements, genealogy edges and the order
/// status flip commit in one transaction, so a mid-fan-out failure rolls
/// back the whole batch.
/// </summary>
public class CreateProductionConfirmationAtomicityTests
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
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Pass-through fake: executes the callback inline so the handler logic
    /// runs, while recording whether a transaction boundary was used. Real
    /// rollback is proven by the endpoint integration tests against PostgreSQL;
    /// here we prove the handler wraps all four writes in one boundary and
    /// propagates mid-fan-out failures without partial follow-up writes.
    /// </summary>
    public CreateProductionConfirmationAtomicityTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<ProductionConfirmationResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<ProductionConfirmationResponse>> action, CancellationToken _) => action());
    }

    private CreateProductionConfirmationRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _children.Object, _movements.Object,
            _lots.Object, _edges.Object, _guids.Object, _clock.Object, _tenant.Object, _unitOfWork.Object);

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

    private static CreateProductionConfirmationRequest ValidRequest(Guid orderId) => new(
        orderId,
        Guid.NewGuid(),
        null,
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        10m,
        2m,
        null);

    [Fact]
    public async Task Handle_HappyPath_WrapsAllWritesInSingleTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        await CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(
            It.IsAny<Func<Task<ProductionConfirmationResponse>>>(), It.IsAny<CancellationToken>()), Times.Once);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Once);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        order.Status.Should().Be(ProductionOrderStatus.InProgress);
    }

    [Fact]
    public async Task Handle_MovementFailure_PropagatesAndSkipsOrderStatusFlip()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated movement persistence failure"));

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated movement persistence failure");
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(
            It.IsAny<Func<Task<ProductionConfirmationResponse>>>(), It.IsAny<CancellationToken>()), Times.Once);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        order.Status.Should().Be(ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_EdgeFailure_PropagatesAndSkipsOrderStatusFlip()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lot
            {
                Id = producedId,
                TenantId = _tenantId,
                Code = "LOT-P",
                ProductId = Guid.NewGuid(),
                MeasureUnitId = Guid.NewGuid(),
                Quantity = 100m,
                CreatedAt = _now
            });
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lot
            {
                Id = consumedId,
                TenantId = _tenantId,
                Code = "LOT-C",
                ProductId = Guid.NewGuid(),
                MeasureUnitId = Guid.NewGuid(),
                Quantity = 100m,
                CreatedAt = _now
            });
        _edges.Setup(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated edge persistence failure"));
        var request = ValidRequest(order.Id) with
        {
            ProducedLotId = producedId,
            ConsumedLots = new[] { new ConsumedLotEntry(consumedId, 5m) }
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated edge persistence failure");
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        order.Status.Should().Be(ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_ClosedOrder_WritesNothingAndNeverOpensTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        order.Status = ProductionOrderStatus.Closed;
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(
            It.IsAny<Func<Task<ProductionConfirmationResponse>>>(), It.IsAny<CancellationToken>()), Times.Never);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Never);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
