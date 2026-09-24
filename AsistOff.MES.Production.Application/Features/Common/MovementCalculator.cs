using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Common;

/// <summary>
/// Pure scaling logic for RW/PW movement previews. Kept static so it is
/// trivially unit-testable without a database.
/// </summary>
public static class MovementCalculator
{
    public const string ReceiptType = "PW";
    public const string IssueType = "RW";

    /// <summary>Preview lists are paging-free and capped at 500 rows.</summary>
    public const int MaxLines = 500;

    /// <summary>
    /// Scales one BOM item to a produced quantity, applying
    /// <see cref="BomItem.ScrapPercentage"/> as an uplift factor:
    /// <list type="bullet">
    /// <item><see cref="BomQuantityType.PerUnit"/> scales linearly with the produced quantity.</item>
    /// <item><see cref="BomQuantityType.PerBatch"/> is consumed once per preview scope (one batch).</item>
    /// <item><see cref="BomQuantityType.PerOperationRun"/> is consumed once per operation run
    /// (<paramref name="runCount"/> confirmations aggregated in the preview).</item>
    /// </list>
    /// </summary>
    public static decimal ScaleBomQuantity(BomItem item, decimal producedQuantity, int runCount)
    {
        var factor = 1m + (item.ScrapPercentage ?? 0m) / 100m;

        return item.QuantityType switch
        {
            BomQuantityType.PerUnit => item.Quantity * producedQuantity * factor,
            BomQuantityType.PerBatch => item.Quantity * factor,
            BomQuantityType.PerOperationRun => item.Quantity * Math.Max(1, runCount) * factor,
            _ => item.Quantity * producedQuantity * factor
        };
    }

    /// <summary>
    /// Builds the preview for an order: one PW line with the produced
    /// quantity of the order product, followed by RW lines scaled from the
    /// released recipe BOM items. The result is capped at
    /// <see cref="MaxLines"/> rows (PW first).
    /// </summary>
    public static IReadOnlyList<MovementPreviewLine> BuildPreview(
        ProductionOrder order,
        decimal producedQuantity,
        int runCount,
        IReadOnlyCollection<BomItem> bomItems)
    {
        var lines = new List<MovementPreviewLine>(bomItems.Count + 1)
        {
            new(ReceiptType, order.ProductId, producedQuantity, order.MeasureUnitId, null)
        };

        foreach (var item in bomItems
                     .OrderBy(x => x.SortIndex)
                     .ThenBy(x => x.Id)
                     .Take(MaxLines - 1))
        {
            lines.Add(new MovementPreviewLine(
                IssueType,
                item.ProductId,
                ScaleBomQuantity(item, producedQuantity, runCount),
                item.MeasureUnitId,
                item.PreferredWarehouseId));
        }

        return lines;
    }
}
