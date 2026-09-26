using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
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
/// Atomicity tests for the operator confirmation fan-out (issue #265): the
/// confirmation row, RW/PW movements, genealogy edges and the order status
/// flip commit in a single <see cref="IUnitOfWork"/> transaction. A mid-fan-out
/// failure propagates and leaves no partial writes behind (the real database
/// rolls the transaction back; the handler additionally restores the
/// in-memory order status).
/// </summary>
public sealed class CreateProductionConfirmationAtomicityTests
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
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public CreateProductionConfirmationAtomicityTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        // Inline transaction: run the fan-out delegate directly so the tests
        // observe production write order; the real DefaultContextUnitOfWork
        // wraps the same delegate in BeginTransaction/Commit (rollback on throw).
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
    }

    private CreateProductionConfirmationRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _children.Object, _movements.Object,
            _lots.Object, _edges.Object, _guids.Object, _clock.Object, _tenant.Object, _uow.Object);

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

    private static Lot NewLot(Guid id) => new()
    {
        Id = id,
        TenantId = Guid.NewGuid(),
        Code = $"LOT-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
        ProductId = Guid.NewGuid(),
        MeasureUnitId = Guid.NewGuid(),
        Quantity = 100m,
        Status = LotStatus.Available,
        CreatedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private CreateProductionConfirmationRequest ValidRequest(Guid orderId) => new(
        orderId,
        Guid.NewGuid(),
        null,
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        10m,
        2m,
        null);

    [Fact]
    public async Task Handle_HappyPath_RunsAllWritesInsideSingleTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(consumedId));
        var request = new CreateProductionConfirmationRequest(
            order.Id, Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc), 10m, 0m, null,
            producedId, new[] { new ConsumedLotEntry(consumedId, 5m) });

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert — all four writes ran inside exactly one transaction.
        _uow.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Once);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Once);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        order.Status.Should().Be(ProductionOrderStatus.InProgress);
        result.ProductionOrderId.Should().Be(order.Id);
    }

    [Fact]
    public async Task Handle_HappyPath_PersistedLinesMatchPreview()
    {
        // Arrange
        var order = ReleasedOrder();
        order.MeasureUnitId = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var bomProductId = Guid.NewGuid();
        var bomMeasureUnitId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var bomItems = new List<BomItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OperationNodeId = Guid.NewGuid(),
                ProductId = bomProductId,
                MeasureUnitId = bomMeasureUnitId,
                Quantity = 2m,
                QuantityType = BomQuantityType.PerUnit,
                PreferredWarehouseId = warehouseId,
                SortIndex = 0
            }
        };
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bomItems);
        IReadOnlyCollection<StockMovement>? posted = null;
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<StockMovement>, CancellationToken>((e, _) => posted = e)
            .Returns(Task.CompletedTask);

        // Act
        await CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert — persisted lines equal MovementCalculator.BuildPreview lines.
        var expected = MovementCalculator.BuildPreview(order, 10m, 1, bomItems);
        posted.Should().NotBeNull();
        posted.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            posted!.ElementAt(i).MovementType.Should().Be(expected[i].MovementType);
            posted!.ElementAt(i).ProductId.Should().Be(expected[i].ProductId);
            posted!.ElementAt(i).Quantity.Should().Be(expected[i].Quantity);
            posted!.ElementAt(i).MeasureUnitId.Should().Be(expected[i].MeasureUnitId);
            posted!.ElementAt(i).WarehouseId.Should().Be(expected[i].PreferredWarehouseId);
        }
    }

    [Fact]
    public async Task Handle_MovementFailure_PropagatesAndLeavesOrderReleased()
    {
        // Arrange — the movement insert fails after the confirmation row was
        // staged; the transaction rolls back so no orphan confirmation survives.
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("movement insert failed"));

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _uow.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Once);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        order.Status.Should().Be(ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_EdgeFailure_PropagatesAndLeavesOrderReleased()
    {
        // Arrange — the genealogy edge insert fails mid-fan-out.
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(consumedId));
        _edges.Setup(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("edge insert failed"));
        var request = new CreateProductionConfirmationRequest(
            order.Id, Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc), 10m, 0m, null,
            producedId, new[] { new ConsumedLotEntry(consumedId, 5m) });

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        order.Status.Should().Be(ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_ClosedOrder_ThrowsConflictAndNeverOpensTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        order.Status = ProductionOrderStatus.Closed;
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert — conflict with zero writes and no transaction.
        await act.Should().ThrowAsync<ConflictException>();
        _uow.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Never);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Never);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownLot_ThrowsNotFoundAndNeverOpensTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);
        var request = new CreateProductionConfirmationRequest(
            order.Id, Guid.NewGuid(), null,
            new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc), 10m, 0m, null,
            producedId, new[] { new ConsumedLotEntry(Guid.NewGuid(), 5m) });

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _uow.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Never);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
