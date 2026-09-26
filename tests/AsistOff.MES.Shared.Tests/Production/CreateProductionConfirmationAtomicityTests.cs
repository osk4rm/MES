using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Proves the operator confirmation fan-out is atomic (issue #265): the
/// confirmation row, RW/PW movements, genealogy edges, and the order status
/// flip share one <see cref="IProductionUnitOfWork"/> transaction, so any
/// mid-fan-out failure rolls everything back and failed validations write
/// nothing at all.
/// </summary>
public class CreateProductionConfirmationAtomicityTests
{
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();
    private readonly Mock<IStockMovementsRepository> _movements = new();
    private readonly Mock<ILotsRepository> _lots = new();
    private readonly Mock<ILotGenealogyEdgesRepository> _edges = new();
    private readonly Mock<IProductionUnitOfWork> _unitOfWork = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    public CreateProductionConfirmationAtomicityTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        // Pass-through: execute the transactional delegate inline. The real
        // ProductionUnitOfWork opens a database transaction around the same
        // delegate, so a throw inside rolls back every staged write.
        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
    }

    private CreateProductionConfirmationRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _children.Object, _movements.Object,
            _lots.Object, _edges.Object, _unitOfWork.Object, _guids.Object, _clock.Object, _tenant.Object);

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
        Guid.NewGuid(),
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        10m,
        0m,
        null);

    [Fact]
    public async Task Handle_HappyPath_PersistsAllWritesInSingleTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(consumedId));
        ProductionConfirmation? saved = null;
        _confirmations.Setup(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()))
            .Callback<ProductionConfirmation, CancellationToken>((e, _) => saved = e)
            .ReturnsAsync((ProductionConfirmation e, CancellationToken _) => e);
        var request = ValidRequest(order.Id) with
        {
            ProducedLotId = producedId,
            ConsumedLots = new[] { new ConsumedLotEntry(consumedId, 5m) }
        };

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        result.ProductionOrderId.Should().Be(order.Id);
        saved.Should().NotBeNull();
        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Once);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Once);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        order.Status.Should().Be(ProductionOrderStatus.InProgress);
    }

    [Fact]
    public async Task Handle_HappyPath_PersistedLinesMatchPreview()
    {
        // Arrange
        var order = ReleasedOrder();
        order.MeasureUnitId = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var bomItems = new List<BomItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                OperationNodeId = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                MeasureUnitId = Guid.NewGuid(),
                Quantity = 2m,
                QuantityType = BomQuantityType.PerUnit,
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

        // Assert
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
    public async Task Handle_MovementFailure_PropagatesAndSkipsOrderUpdate()
    {
        // Arrange — the staged confirmation Add succeeds, then the movement
        // write throws. The real ProductionUnitOfWork rolls back the staged
        // confirmation via the database transaction, so no orphan row
        // survives; here we prove the failure propagates inside the
        // transaction and the order flip never runs.
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("movement insert failed"));

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Once);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        order.Status.Should().Be(ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_EdgeFailure_PropagatesAndSkipsOrderUpdate()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(consumedId));
        _edges.Setup(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("edge insert failed"));
        var request = ValidRequest(order.Id) with
        {
            ProducedLotId = producedId,
            ConsumedLots = new[] { new ConsumedLotEntry(consumedId, 5m) }
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        order.Status.Should().Be(ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_ClosedOrder_WritesNothing()
    {
        // Arrange
        var order = ReleasedOrder();
        order.Status = ProductionOrderStatus.Closed;
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var act = () => CreateSut().Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Never);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownConsumedLot_WritesNothing()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var producedId = Guid.NewGuid();
        var consumedId = Guid.NewGuid();
        _lots.Setup(r => r.GetAsync(producedId, It.IsAny<CancellationToken>())).ReturnsAsync(NewLot(producedId));
        _lots.Setup(r => r.GetAsync(consumedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lot?)null);
        var request = ValidRequest(order.Id) with
        {
            ProducedLotId = producedId,
            ConsumedLots = new[] { new ConsumedLotEntry(consumedId, 5m) }
        };

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Never);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
