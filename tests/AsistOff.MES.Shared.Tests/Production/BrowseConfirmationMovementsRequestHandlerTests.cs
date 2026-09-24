using AsistOff.MES.Production.Application.Features.ProductionConfirmations.Movements;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class BrowseConfirmationMovementsRequestHandlerTests
{
    private readonly Mock<IProductionConfirmationsRepository> _confirmations = new();
    private readonly Mock<IProductionOrdersRepository> _orders = new();
    private readonly Mock<IChildEntitiesRepository> _children = new();

    private BrowseConfirmationMovementsRequestHandler CreateSut() =>
        new(_confirmations.Object, _orders.Object, _children.Object);

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
        ScrapQuantity = 1m
    };

    private static BomItem BomItem(decimal quantity, BomQuantityType type, decimal? scrap = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OperationNodeId = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        Quantity = quantity,
        QuantityType = type,
        ScrapPercentage = scrap,
        PreferredWarehouseId = Guid.NewGuid()
    };

    [Fact]
    public async Task Handle_HappyPath_ReturnsPwPlusScaledRwLines()
    {
        var order = Order();
        var confirmation = Confirmation(order.Id, 10m);
        var perUnit = BomItem(2m, BomQuantityType.PerUnit);
        _confirmations.Setup(r => r.GetAsync(confirmation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmation);
        _orders.Setup(r => r.GetAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _children.Setup(r => r.ListBomItemsForVersionAsync(order.RecipeVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([perUnit]);

        var lines = await CreateSut().Handle(new BrowseConfirmationMovementsRequest(confirmation.Id), CancellationToken.None);

        lines.Should().HaveCount(2);
        lines[0].MovementType.Should().Be("PW");
        lines[0].ProductId.Should().Be(order.ProductId);
        lines[0].Quantity.Should().Be(10m);
        lines[1].MovementType.Should().Be("RW");
        lines[1].ProductId.Should().Be(perUnit.ProductId);
        lines[1].Quantity.Should().Be(20m);
        lines[1].PreferredWarehouseId.Should().Be(perUnit.PreferredWarehouseId);
    }

    [Fact]
    public async Task Handle_UnknownConfirmation_ThrowsNotFoundException()
    {
        _confirmations.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionConfirmation?)null);

        var act = () => CreateSut().Handle(new BrowseConfirmationMovementsRequest(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_MissingOrder_ThrowsNotFoundException()
    {
        var confirmation = Confirmation(Guid.NewGuid(), 5m);
        _confirmations.Setup(r => r.GetAsync(confirmation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmation);
        _orders.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductionOrder?)null);

        var act = () => CreateSut().Handle(new BrowseConfirmationMovementsRequest(confirmation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
