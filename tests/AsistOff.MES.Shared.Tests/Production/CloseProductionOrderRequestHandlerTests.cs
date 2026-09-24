using AsistOff.MES.Production.Application.Features.ProductionOrders.Close;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CloseProductionOrderRequestHandlerTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 16, 0, 0, DateTimeKind.Utc);

    public CloseProductionOrderRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private CloseProductionOrderRequestHandler CreateSut() =>
        new(_orders.Object, _confirmations.Object, _clock.Object);

    private static ProductionOrder MakeOrder(ProductionOrderStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = status,
        CreatedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc),
        UpdatedAt = status == ProductionOrderStatus.Completed
            ? new DateTime(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc)
            : null
    };

    [Fact]
    public async Task Handle_CompletedOrder_ClosesOrderWithTotals()
    {
        var order = MakeOrder(ProductionOrderStatus.Completed);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _confirmations.Setup(r => r.GetTotalsAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((60m, 5m, 3));

        var result = await CreateSut().Handle(new CloseProductionOrderRequest(order.Id), CancellationToken.None);

        order.Status.Should().Be(ProductionOrderStatus.Closed);
        order.UpdatedAt.Should().Be(_now);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        result.Status.Should().Be(ProductionOrderStatus.Closed);
        result.ProducedQuantity.Should().Be(60m);
        result.ScrappedQuantity.Should().Be(5m);
        result.RemainingQuantity.Should().Be(40m);
        result.ConfirmationsCount.Should().Be(3);
        result.ClosedAt.Should().Be(_now);
        result.CompletedAt.Should().Be(_now);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Planned)]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.InProgress)]
    [InlineData(ProductionOrderStatus.Closed)]
    public async Task Handle_WrongStatus_ThrowsConflictException(ProductionOrderStatus status)
    {
        var order = MakeOrder(status);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var act = () => CreateSut().Handle(new CloseProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateSut().Handle(new CloseProductionOrderRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
