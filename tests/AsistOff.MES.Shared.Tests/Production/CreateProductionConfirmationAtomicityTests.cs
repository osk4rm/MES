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
/// Atomicity coverage for the operator confirmation fan-out (issue #265):
/// the confirmation row, all RW/PW movements, all genealogy edges and the
/// order status flip share one <see cref="IUnitOfWork"/> transaction, so a
/// mid-fan-out failure rolls everything back and failed validations open no
/// transaction at all.
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
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<ProductionConfirmation> _savedConfirmations = new();
    private readonly List<StockMovement> _savedMovements = new();
    private readonly List<LotGenealogyEdge> _savedEdges = new();

    public CreateProductionConfirmationAtomicityTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());

        _confirmations.Setup(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()))
            .Callback<ProductionConfirmation, CancellationToken>((e, _) => _savedConfirmations.Add(e))
            .ReturnsAsync((ProductionConfirmation e, CancellationToken _) => e);
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<StockMovement>, CancellationToken>((e, _) => _savedMovements.AddRange(e))
            .Returns(Task.CompletedTask);
        _edges.Setup(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()))
            .Callback<LotGenealogyEdge, CancellationToken>((e, _) => _savedEdges.Add(e))
            .ReturnsAsync((LotGenealogyEdge e, CancellationToken _) => e);
    }

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

    private CreateProductionConfirmationRequest ValidRequest(Guid orderId) => new(
        orderId,
        Guid.NewGuid(),
        null,
        new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        10m,
        2m,
        null);

    /// <summary>
    /// Snapshot/restore <see cref="IUnitOfWork"/> double: executes the fan-out
    /// delegate inline and, on failure, truncates the backing stores and
    /// restores the order status — the in-memory equivalent of a database
    /// rollback, proving the handler runs all four writes inside one
    /// transaction boundary.
    /// </summary>
    private sealed class SnapshotUnitOfWork(
        List<ProductionConfirmation> confirmations,
        List<StockMovement> movements,
        List<LotGenealogyEdge> edges,
        ProductionOrder order) : IUnitOfWork
    {
        public int Calls { get; private set; }

        public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync<object?>(async ct =>
            {
                await action(ct);
                return null;
            }, cancellationToken);
        }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        {
            Calls++;

            var confirmationsCount = confirmations.Count;
            var movementsCount = movements.Count;
            var edgesCount = edges.Count;
            var orderStatus = order.Status;

            try
            {
                return await action(cancellationToken);
            }
            catch
            {
                while (confirmations.Count > confirmationsCount)
                    confirmations.RemoveAt(confirmations.Count - 1);
                while (movements.Count > movementsCount)
                    movements.RemoveAt(movements.Count - 1);
                while (edges.Count > edgesCount)
                    edges.RemoveAt(edges.Count - 1);
                order.Status = orderStatus;
                throw;
            }
        }
    }

    private static Mock<IUnitOfWork> InlineUnitOfWork()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));
        unitOfWork.Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<ProductionConfirmationResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<ProductionConfirmationResponse>> action, CancellationToken ct) => action(ct));
        return unitOfWork;
    }

    private CreateProductionConfirmationRequestHandler CreateSut(IUnitOfWork unitOfWork) =>
        new(_confirmations.Object, _orders.Object, _children.Object, _movements.Object,
            _lots.Object, _edges.Object, _guids.Object, _clock.Object, _tenant.Object, unitOfWork);

    [Fact]
    public async Task Handle_HappyPath_PersistsAllWritesInsideSingleTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var unitOfWork = new SnapshotUnitOfWork(_savedConfirmations, _savedMovements, _savedEdges, order);

        // Act
        var result = await CreateSut(unitOfWork).Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        unitOfWork.Calls.Should().Be(1);
        _savedConfirmations.Should().ContainSingle(c => c.Id == result.Id && c.ProductionOrderId == order.Id);
        _savedMovements.Should().NotBeEmpty();
        _savedMovements.Should().OnlyContain(m =>
            m.ProductionConfirmationId == result.Id && m.ProductionOrderId == order.Id);
        order.Status.Should().Be(ProductionOrderStatus.InProgress);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MovementFailure_RollsBackConfirmation()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _movements.Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<StockMovement>, CancellationToken>((e, _) => _savedMovements.AddRange(e))
            .ThrowsAsync(new InvalidOperationException("simulated movement persistence failure"));
        var unitOfWork = new SnapshotUnitOfWork(_savedConfirmations, _savedMovements, _savedEdges, order);

        // Act
        var act = () => CreateSut(unitOfWork).Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _savedConfirmations.Should().BeEmpty("the confirmation row must roll back with the failed movements");
        _savedMovements.Should().BeEmpty();
        _savedEdges.Should().BeEmpty();
        order.Status.Should().Be(ProductionOrderStatus.Released);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EdgeFailure_RollsBackConfirmationAndMovements()
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
                Status = LotStatus.Available,
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
                Status = LotStatus.Available,
                CreatedAt = _now
            });
        _edges.Setup(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated edge persistence failure"));
        var unitOfWork = new SnapshotUnitOfWork(_savedConfirmations, _savedMovements, _savedEdges, order);
        var request = new CreateProductionConfirmationRequest(
            order.Id, Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc), 10m, 0m, null,
            producedId, new[] { new ConsumedLotEntry(consumedId, 5m) });

        // Act
        var act = () => CreateSut(unitOfWork).Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _savedConfirmations.Should().BeEmpty("the confirmation row must roll back with the failed edge");
        _savedMovements.Should().BeEmpty("persisted RW/PW lines must roll back with the failed edge");
        _savedEdges.Should().BeEmpty();
        order.Status.Should().Be(ProductionOrderStatus.Released);
    }

    [Fact]
    public async Task Handle_ClosedOrder_WritesNothingAndOpensNoTransaction()
    {
        // Arrange
        var order = ReleasedOrder();
        order.Status = ProductionOrderStatus.Closed;
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var unitOfWorkMock = InlineUnitOfWork();

        // Act
        var act = () => CreateSut(unitOfWorkMock.Object).Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _savedConfirmations.Should().BeEmpty();
        _savedMovements.Should().BeEmpty();
        _savedEdges.Should().BeEmpty();
        _confirmations.Verify(r => r.AddAsync(It.IsAny<ProductionConfirmation>(), It.IsAny<CancellationToken>()), Times.Never);
        _movements.Verify(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<StockMovement>>(), It.IsAny<CancellationToken>()), Times.Never);
        _edges.Verify(r => r.AddAsync(It.IsAny<LotGenealogyEdge>(), It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(
            u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PersistedLines_EqualMovementPreview()
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
        var unitOfWork = new SnapshotUnitOfWork(_savedConfirmations, _savedMovements, _savedEdges, order);

        // Act
        await CreateSut(unitOfWork).Handle(ValidRequest(order.Id), CancellationToken.None);

        // Assert
        var expected = MovementCalculator.BuildPreview(order, 10m, 1, bomItems);
        _savedMovements.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            _savedMovements[i].MovementType.Should().Be(expected[i].MovementType);
            _savedMovements[i].ProductId.Should().Be(expected[i].ProductId);
            _savedMovements[i].Quantity.Should().Be(expected[i].Quantity);
            _savedMovements[i].MeasureUnitId.Should().Be(expected[i].MeasureUnitId);
            _savedMovements[i].WarehouseId.Should().Be(expected[i].PreferredWarehouseId);
        }
    }
}
