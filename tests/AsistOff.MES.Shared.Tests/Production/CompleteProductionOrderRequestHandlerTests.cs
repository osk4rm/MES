using AsistOff.MES.Production.Application.Features.ProductionOrders.Complete;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class CompleteProductionOrderRequestHandlerTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly DateTime _now = new(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc);

    public CompleteProductionOrderRequestHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(_now);
    }

    private CompleteProductionOrderRequestHandler CreateSut() =>
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
        CreatedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc)
    };

    private void SetupOrder(ProductionOrder order) =>
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

    private void SetupTotals(Guid orderId, decimal produced, decimal scrapped, int count) =>
        _confirmations.Setup(r => r.GetTotalsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((produced, scrapped, count));

    [Fact]
    public async Task Handle_InProgressWithGoodQuantity_CompletesOrderWithTotals()
    {
        var order = MakeOrder(ProductionOrderStatus.InProgress);
        SetupOrder(order);
        SetupTotals(order.Id, 60m, 5m, 3);

        var result = await CreateSut().Handle(new CompleteProductionOrderRequest(order.Id), CancellationToken.None);

        order.Status.Should().Be(ProductionOrderStatus.Completed);
        order.UpdatedAt.Should().Be(_now);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        result.Status.Should().Be(ProductionOrderStatus.Completed);
        result.ProducedQuantity.Should().Be(60m);
        result.ScrappedQuantity.Should().Be(5m);
        result.RemainingQuantity.Should().Be(40m);
        result.ConfirmationsCount.Should().Be(3);
        result.CompletedAt.Should().Be(_now);
        result.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_InProgressWithoutGoodQuantity_ThrowsConflictException()
    {
        var order = MakeOrder(ProductionOrderStatus.InProgress);
        SetupOrder(order);
        SetupTotals(order.Id, 0m, 4m, 2);

        var act = () => CreateSut().Handle(new CompleteProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Planned)]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Closed)]
    public async Task Handle_WrongStatus_ThrowsConflictException(ProductionOrderStatus status)
    {
        var order = MakeOrder(status);
        SetupOrder(order);

        var act = () => CreateSut().Handle(new CompleteProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _orders.Verify(r => r.UpdateAsync(It.IsAny<ProductionOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateSut().Handle(new CompleteProductionOrderRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CrossTenantOrder_ThrowsNotFoundException()
    {
        // The tenant global query filter hides other tenants' rows, so the
        // repository returns null and the handler maps it to 404.
        var id = Guid.NewGuid();
        _orders.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateSut().Handle(new CompleteProductionOrderRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _confirmations.Verify(r => r.GetTotalsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
