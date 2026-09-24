using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using FluentAssertions;

namespace AsistOff.MES.Shared.Tests.Production;

public class MovementCalculatorTests
{
    private static BomItem Item(
        BomQuantityType type,
        decimal quantity,
        decimal? scrapPercentage = null,
        int sortIndex = 0) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OperationNodeId = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        Quantity = quantity,
        QuantityType = type,
        ScrapPercentage = scrapPercentage,
        SortIndex = sortIndex
    };

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

    [Fact]
    public void ScaleBomQuantity_PerUnit_MultipliesByProducedQuantity()
    {
        var item = Item(BomQuantityType.PerUnit, 2m);

        var scaled = MovementCalculator.ScaleBomQuantity(item, 10m, 1);

        scaled.Should().Be(20m);
    }

    [Fact]
    public void ScaleBomQuantity_PerUnit_AppliesScrapPercentage()
    {
        var item = Item(BomQuantityType.PerUnit, 2m, scrapPercentage: 10m);

        var scaled = MovementCalculator.ScaleBomQuantity(item, 10m, 1);

        scaled.Should().Be(22m);
    }

    [Fact]
    public void ScaleBomQuantity_PerBatch_IgnoresProducedQuantity()
    {
        var item = Item(BomQuantityType.PerBatch, 5m, scrapPercentage: 20m);

        var scaled = MovementCalculator.ScaleBomQuantity(item, 100m, 3);

        scaled.Should().Be(6m);
    }

    [Fact]
    public void ScaleBomQuantity_PerOperationRun_MultipliesByRunCount()
    {
        var item = Item(BomQuantityType.PerOperationRun, 1.5m);

        var scaled = MovementCalculator.ScaleBomQuantity(item, 100m, 3);

        scaled.Should().Be(4.5m);
    }

    [Fact]
    public void BuildPreview_ReturnsPwLinePlusScaledRwLines()
    {
        var order = Order();
        var perUnit = Item(BomQuantityType.PerUnit, 2m, sortIndex: 0);
        var perBatch = Item(BomQuantityType.PerBatch, 5m, sortIndex: 1);

        var lines = MovementCalculator.BuildPreview(order, 10m, 1, [perUnit, perBatch]);

        lines.Should().HaveCount(3);
        lines[0].Should().Be(new MovementPreviewLine("PW", order.ProductId, 10m, order.MeasureUnitId, null));
        lines[1].MovementType.Should().Be("RW");
        lines[1].ProductId.Should().Be(perUnit.ProductId);
        lines[1].Quantity.Should().Be(20m);
        lines[2].Quantity.Should().Be(5m);
        lines[2].PreferredWarehouseId.Should().Be(perBatch.PreferredWarehouseId);
    }

    [Fact]
    public void BuildPreview_CapsAtMaxLines()
    {
        var order = Order();
        var items = Enumerable.Range(0, MovementCalculator.MaxLines + 50)
            .Select(i => Item(BomQuantityType.PerUnit, 1m, sortIndex: i))
            .ToList();

        var lines = MovementCalculator.BuildPreview(order, 1m, 1, items);

        lines.Should().HaveCount(MovementCalculator.MaxLines);
        lines[0].MovementType.Should().Be("PW");
    }
}
