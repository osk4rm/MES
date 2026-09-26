using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Close;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

/// <summary>
/// Close-time settlement coverage (issue #291): closing an order closes any
/// remaining open reservations so they stop reducing stock availability.
/// </summary>
public sealed class CloseProductionOrderReservationsTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IMaterialReservationsRepository> _reservations = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly DateTime _now = new(2026, 9, 26, 16, 0, 0, DateTimeKind.Utc);

    public CloseProductionOrderReservationsTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
        _reservations.Setup(r => r.ListForOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation>());
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> op, CancellationToken _) => op());
    }

    private CloseProductionOrderRequestHandler CreateSut() =>
        new(_orders.Object, _confirmations.Object, _reservations.Object, _clock.Object, _uow.Object);

    private static ProductionOrder CompletedOrder() => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = ProductionOrderStatus.Completed
    };

    private void SetupOrder(ProductionOrder order)
    {
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _confirmations.Setup(r => r.GetTotalsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((60m, 5m, 1));
    }

    [Fact]
    public async Task Handle_WithOpenReservations_ClosesThem()
    {
        // Arrange
        var order = CompletedOrder();
        SetupOrder(order);
        var active = new MaterialReservation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductionOrderId = order.Id,
            ProductId = Guid.NewGuid(),
            QuantityReserved = 200m,
            QuantityRelieved = 20m,
            Status = ReservationStatus.Active,
            CreatedAt = _now.AddHours(-4)
        };
        var partial = new MaterialReservation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductionOrderId = order.Id,
            ProductId = Guid.NewGuid(),
            QuantityReserved = 100m,
            QuantityRelieved = 40m,
            Status = ReservationStatus.PartiallyRelieved,
            CreatedAt = _now.AddHours(-4)
        };
        var closed = new MaterialReservation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductionOrderId = order.Id,
            ProductId = Guid.NewGuid(),
            QuantityReserved = 50m,
            QuantityRelieved = 50m,
            Status = ReservationStatus.Closed,
            CreatedAt = _now.AddHours(-4)
        };
        _reservations.Setup(r => r.ListForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation> { active, partial, closed });

        // Act
        var result = await CreateSut().Handle(new CloseProductionOrderRequest(order.Id), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ProductionOrderStatus.Closed);
        active.Status.Should().Be(ReservationStatus.Closed);
        partial.Status.Should().Be(ReservationStatus.Closed);
        active.UpdatedAt.Should().Be(_now);
        _reservations.Verify(r => r.UpdateAsync(active, It.IsAny<CancellationToken>()), Times.Once);
        _reservations.Verify(r => r.UpdateAsync(partial, It.IsAny<CancellationToken>()), Times.Once);
        _reservations.Verify(r => r.UpdateAsync(closed, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutReservations_OnlyUpdatesOrder()
    {
        // Arrange
        var order = CompletedOrder();
        SetupOrder(order);

        // Act
        var result = await CreateSut().Handle(new CloseProductionOrderRequest(order.Id), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ProductionOrderStatus.Closed);
        _reservations.Verify(
            r => r.UpdateAsync(It.IsAny<MaterialReservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_TransactionFails_RestoresOrderAndReservations()
    {
        // Arrange — the fan-out transaction throws so the rolled-back
        // in-memory Closed mutations must be restored on tracked instances.
        var order = CompletedOrder();
        SetupOrder(order);
        var active = new MaterialReservation
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProductionOrderId = order.Id,
            ProductId = Guid.NewGuid(),
            QuantityReserved = 200m,
            QuantityRelieved = 20m,
            Status = ReservationStatus.Active,
            CreatedAt = _now.AddHours(-4),
            UpdatedAt = null
        };
        _reservations.Setup(r => r.ListForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MaterialReservation> { active });
        _uow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        // Act
        var act = () => CreateSut().Handle(new CloseProductionOrderRequest(order.Id), CancellationToken.None);

        // Assert — the exception propagates and tracked state is restored.
        await act.Should().ThrowAsync<InvalidOperationException>();
        order.Status.Should().Be(ProductionOrderStatus.Completed);
        order.UpdatedAt.Should().BeNull();
        active.Status.Should().Be(ReservationStatus.Active);
        active.UpdatedAt.Should().BeNull();
    }
}
