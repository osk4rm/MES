using AsistOff.MES.Production.Application.Features.ProductionOrders.Delete;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class DeleteProductionOrderRequestHandlerTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();

    private DeleteProductionOrderRequestHandler CreateSut() => new(_orders.Object);

    [Fact]
    public async Task Throws_ConflictException_when_order_is_released()
    {
        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(),
            Code = "PO-001",
            ProductId = Guid.NewGuid(),
            RecipeId = Guid.NewGuid(),
            RecipeVersionId = Guid.NewGuid(),
            PlannedQuantity = 10m,
            Status = ProductionOrderStatus.Released
        };
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var act = () => CreateSut().Handle(new DeleteProductionOrderRequest(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Deletes_order_when_planned()
    {
        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(),
            Code = "PO-001",
            ProductId = Guid.NewGuid(),
            RecipeId = Guid.NewGuid(),
            RecipeVersionId = Guid.NewGuid(),
            PlannedQuantity = 10m,
            Status = ProductionOrderStatus.Planned
        };
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        await CreateSut().Handle(new DeleteProductionOrderRequest(order.Id), CancellationToken.None);

        _orders.Verify(r => r.DeleteAsync(order.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
