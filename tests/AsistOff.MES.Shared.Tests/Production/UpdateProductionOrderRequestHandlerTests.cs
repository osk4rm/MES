using AsistOff.MES.Production.Application.Features.ProductionOrders.Update;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class UpdateProductionOrderRequestHandlerTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();

    private UpdateProductionOrderRequestHandler CreateSut() => new(_orders.Object);

    private static ProductionOrder MakeOrder(ProductionOrderStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 10m,
        Status = status
    };

    private static UpdateProductionOrderRequest ToRequest(ProductionOrder o, string? code = null) =>
        new(o.Id, code ?? o.Code, o.ProductId, o.RecipeId, o.RecipeVersionId, 25m, null, 1, null, "notes", null);

    [Fact]
    public async Task Throws_ConflictException_when_order_is_released()
    {
        var order = MakeOrder(ProductionOrderStatus.Released);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var act = () => CreateSut().Handle(ToRequest(order), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Persists_changes_when_order_is_planned()
    {
        var order = MakeOrder(ProductionOrderStatus.Planned);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orders.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateSut().Handle(ToRequest(order), CancellationToken.None);

        order.PlannedQuantity.Should().Be(25m);
        order.Notes.Should().Be("notes");
        result.PlannedQuantity.Should().Be(25m);
        _orders.Verify(r => r.UpdateAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }
}
