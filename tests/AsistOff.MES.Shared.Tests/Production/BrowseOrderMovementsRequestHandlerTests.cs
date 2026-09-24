using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.ProductionOrders.Movements;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseOrderMovementsRequestHandlerTests
{
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();

    private BrowseOrderMovementsRequestHandler CreateSut() =>
        new(_orders.Object, _confirmations.Object, _children.Object);

    private static ProductionOrder Order() => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = 100m,
        Status = ProductionOrderStatus.Released
    };

    private static ProductionConfirmation Confirmation(Guid orderId, decimal goodQuantity) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ProductionOrderId = orderId,
        MachineId = Guid.NewGuid(),
        ReportedAt = DateTime.UtcNow,
        GoodQuantity = goodQuantity,
        ScrapQuantity = 0m
    };

    private static BomItem BomItem(decimal quantity, BomQuantityType type, int sortIndex = 0) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OperationNodeId = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        Quantity = quantity,
        QuantityType = type,
        SortIndex = sortIndex
    };

    [Fact]
    public async Task Handle_HappyPath_AggregatesGoodQuantityAndScalesBom()
    {
        var order = Order();
        var perUnit = BomItem(2m, BomQuantityType.PerUnit, sortIndex: 0);
        var perRun = BomItem(1m, BomQuantityType.PerOperationRun, sortIndex: 1);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _confirmations.Setup(r => r.ListForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Confirmation(order.Id, 10m), Confirmation(order.Id, 5m)]);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([perUnit, perRun]);

        var lines = await CreateSut().Handle(new BrowseOrderMovementsRequest(order.Id), CancellationToken.None);

        lines.Should().HaveCount(3);
        lines[0].MovementType.Should().Be("PW");
        lines[0].Quantity.Should().Be(15m);
        lines[1].Quantity.Should().Be(30m);
        lines[2].Quantity.Should().Be(2m);
    }

    [Fact]
    public async Task Handle_NoConfirmations_ReturnsZeroPwLine()
    {
        var order = Order();
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _confirmations.Setup(r => r.ListForOrderAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var lines = await CreateSut().Handle(new BrowseOrderMovementsRequest(order.Id), CancellationToken.None);

        lines.Should().ContainSingle()
            .Which.Should().Match<MovementPreviewLine>(l => l.MovementType == "PW" && l.Quantity == 0m);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFoundException()
    {
        // The tenant global query filter hides cross-tenant orders, so both
        // unknown and cross-tenant ids surface as null here.
        _orders.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateSut().Handle(new BrowseOrderMovementsRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
