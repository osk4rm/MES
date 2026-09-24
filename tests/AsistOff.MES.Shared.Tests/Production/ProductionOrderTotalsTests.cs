using AsistOff.MES.Production.Application.Features.ProductionOrders;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class ProductionOrderTotalsTests
{
    private static ProductionOrder MakeOrder(ProductionOrderStatus status, decimal planned = 100m, DateTime? updatedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = "PO-001",
        ProductId = Guid.NewGuid(),
        RecipeId = Guid.NewGuid(),
        RecipeVersionId = Guid.NewGuid(),
        PlannedQuantity = planned,
        Status = status,
        CreatedAt = new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc),
        UpdatedAt = updatedAt
    };

    [Fact]
    public void Map_SumsTotalsAndComputesRemaining()
    {
        var order = MakeOrder(ProductionOrderStatus.InProgress);

        var result = ProductionOrderMappers.Map(order, 60m, 5m, 3);

        result.ProducedQuantity.Should().Be(60m);
        result.ScrappedQuantity.Should().Be(5m);
        result.RemainingQuantity.Should().Be(40m);
        result.ConfirmationsCount.Should().Be(3);
    }

    [Fact]
    public void Map_RemainingFloorsAtZeroWhenOverproduced()
    {
        var order = MakeOrder(ProductionOrderStatus.InProgress, planned: 50m);

        var result = ProductionOrderMappers.Map(order, 80m, 2m, 4);

        result.RemainingQuantity.Should().Be(0m);
    }

    [Fact]
    public void Map_OpenOrder_HasNoCompletionTimestamps()
    {
        var order = MakeOrder(
            ProductionOrderStatus.InProgress,
            updatedAt: new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc));

        var result = ProductionOrderMappers.Map(order, 10m, 0m, 1);

        result.CompletedAt.Should().BeNull();
        result.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void Map_CompletedOrder_ExposesCompletedAt()
    {
        var completedAt = new DateTime(2026, 9, 24, 14, 0, 0, DateTimeKind.Utc);
        var order = MakeOrder(ProductionOrderStatus.Completed, updatedAt: completedAt);

        var result = ProductionOrderMappers.Map(order, 100m, 1m, 5);

        result.CompletedAt.Should().Be(completedAt);
        result.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void Map_ClosedOrder_ExposesCompletedAndClosedAt()
    {
        var closedAt = new DateTime(2026, 9, 24, 16, 0, 0, DateTimeKind.Utc);
        var order = MakeOrder(ProductionOrderStatus.Closed, updatedAt: closedAt);

        var result = ProductionOrderMappers.Map(order, 100m, 1m, 5);

        result.CompletedAt.Should().Be(closedAt);
        result.ClosedAt.Should().Be(closedAt);
    }

    [Fact]
    public void Map_NoConfirmations_AllTotalsZero()
    {
        var order = MakeOrder(ProductionOrderStatus.Planned);

        var result = ProductionOrderMappers.Map(order, 0m, 0m, 0);

        result.ProducedQuantity.Should().Be(0m);
        result.ScrappedQuantity.Should().Be(0m);
        result.RemainingQuantity.Should().Be(order.PlannedQuantity);
        result.ConfirmationsCount.Should().Be(0);
    }
}
