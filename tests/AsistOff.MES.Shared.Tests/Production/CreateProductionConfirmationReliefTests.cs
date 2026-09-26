using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Confirmation-time relief coverage (issue #291): posting a confirmation
/// relieves the matching open reservation from its RW lines inside the same
/// fan-out transaction, flipping PartiallyRelieved then Closed.
/// </summary>
public sealed class CreateProductionConfirmationReliefTests
{
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();
    private readonly Mock<IStockMovementsRepository> _movements = new();
    private readonly Mock<ILotsRepository> _lots = new();
    private readonly Mock<ILotGenealogyEdgesRepository> _edges = new();
    private readonly Mock<IMaterialReservationsRepository> _reservations = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc);

    public CreateProductionConfirmationReliefTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
        _children.Setup(r => r.ListBomItemsForVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>());
        _reservations.Setup(r => r.ListForOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation>());
    }

    private CreateProductionConfirmationRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _children.Object, _movements.Object,
            _lots.Object, _edges.Object, _guids.Object, _clock.Object, _tenant.Object, _uow.Object,
            _reservations.Object);

    private ProductionOrder ReleasedOrder() => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = ProductionOrderStatus.Released,
        ReleasedAt = _now.AddHours(-2)
    };

    [Fact]
    public async Task Handle_WithOpenReservation_RelievesFromRwLines()
    {
        // Arrange — 2 pcs per unit over 10 confirmed pcs relieves 20 of 200.
        var order = ReleasedOrder();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    OperationNodeId = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = 2m,
                    QuantityType = BomQuantityType.PerUnit,
                    PreferredWarehouseId = warehouseId
                }
            });
        var reservation = new MaterialReservation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProductionOrderId = order.Id,
            ProductId = productId,
            WarehouseId = warehouseId,
            QuantityReserved = 200m,
            QuantityRelieved = 0m,
            Status = ReservationStatus.Active,
            CreatedAt = _now.AddHours(-1)
        };
        _reservations.Setup(r => r.ListForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation> { reservation });

        // Act
        await CreateSut().Handle(
            new CreateProductionConfirmationRequest(
                order.Id, Guid.NewGuid(), null, _now.AddMinutes(-5), 10m, 0m, null),
            CancellationToken.None);

        // Assert
        reservation.QuantityRelieved.Should().Be(20m);
        reservation.Status.Should().Be(ReservationStatus.PartiallyRelieved);
        reservation.UpdatedAt.Should().Be(_now);
        _reservations.Verify(r => r.UpdateAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FullyRelievedReservation_ClosesIt()
    {
        // Arrange — 2 pcs per unit over 10 confirmed pcs closes the 20 pcs row.
        var order = ReleasedOrder();
        var productId = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    OperationNodeId = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = 2m,
                    QuantityType = BomQuantityType.PerUnit
                }
            });
        var reservation = new MaterialReservation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProductionOrderId = order.Id,
            ProductId = productId,
            WarehouseId = null,
            QuantityReserved = 20m,
            QuantityRelieved = 0m,
            Status = ReservationStatus.Active,
            CreatedAt = _now.AddHours(-1)
        };
        _reservations.Setup(r => r.ListForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation> { reservation });

        // Act
        await CreateSut().Handle(
            new CreateProductionConfirmationRequest(
                order.Id, Guid.NewGuid(), null, _now.AddMinutes(-5), 10m, 0m, null),
            CancellationToken.None);

        // Assert
        reservation.QuantityRelieved.Should().Be(20m);
        reservation.Status.Should().Be(ReservationStatus.Closed);
        reservation.UpdatedAt.Should().Be(_now);
        _reservations.Verify(r => r.UpdateAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutReservations_WritesNoReservationUpdates()
    {
        // Arrange
        var order = ReleasedOrder();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        await CreateSut().Handle(
            new CreateProductionConfirmationRequest(
                order.Id, Guid.NewGuid(), null, _now.AddMinutes(-5), 10m, 0m, null),
            CancellationToken.None);

        // Assert
        _reservations.Verify(
            r => r.UpdateAsync(It.IsAny<MaterialReservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TransactionFails_RestoresOrderAndReservations()
    {
        // Arrange — same relief setup as the partial case, but the fan-out
        // transaction throws so the rolled-back in-memory mutations must be
        // restored on the tracked instances.
        var order = ReleasedOrder();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BomItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    OperationNodeId = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = 2m,
                    QuantityType = BomQuantityType.PerUnit,
                    PreferredWarehouseId = warehouseId
                }
            });
        var reservation = new MaterialReservation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProductionOrderId = order.Id,
            ProductId = productId,
            WarehouseId = warehouseId,
            QuantityReserved = 200m,
            QuantityRelieved = 0m,
            Status = ReservationStatus.Active,
            CreatedAt = _now.AddHours(-1),
            UpdatedAt = null
        };
        _reservations.Setup(r => r.ListForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation> { reservation });
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        // Act
        var act = () => CreateSut().Handle(
            new CreateProductionConfirmationRequest(
                order.Id, Guid.NewGuid(), null, _now.AddMinutes(-5), 10m, 0m, null),
            CancellationToken.None);

        // Assert — the exception propagates and tracked state is restored.
        await act.Should().ThrowAsync<InvalidOperationException>();
        order.Status.Should().Be(ProductionOrderStatus.Released);
        reservation.QuantityRelieved.Should().Be(0m);
        reservation.Status.Should().Be(ReservationStatus.Active);
        reservation.UpdatedAt.Should().BeNull();
    }
}
