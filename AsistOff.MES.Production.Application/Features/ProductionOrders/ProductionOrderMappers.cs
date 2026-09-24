using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders;

internal static class ProductionOrderMappers
{
    public static ProductionOrderResponse Map(ProductionOrder o) => Map(o, 0m, 0m, 0);

    public static ProductionOrderResponse Map(
        ProductionOrder o,
        decimal producedQuantity,
        decimal scrappedQuantity,
        int confirmationsCount)
    {
        var remaining = o.PlannedQuantity - producedQuantity;
        if (remaining < 0)
            remaining = 0;

        // No migration in this slice: CompletedAt/ClosedAt reuse the audit
        // UpdatedAt column. Complete/Close handlers stamp UpdatedAt explicitly
        // so the transition time is visible on the response even with mocked
        // repositories; in production the auditable interceptor stamps it too.
        // Known limitation: after Close, CompletedAt equals the close time
        // because there is no separate column for the completion time.
        DateTime? completedAt = o.Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Closed
            ? o.UpdatedAt
            : null;
        DateTime? closedAt = o.Status == ProductionOrderStatus.Closed
            ? o.UpdatedAt
            : null;

        return new(
            o.Id,
            o.Code,
            o.ProductId,
            o.RecipeId,
            o.RecipeVersionId,
            o.PlannedQuantity,
            o.MeasureUnitId,
            o.Priority,
            o.DueDate,
            o.Status,
            o.ReleasedAt,
            o.ReleasedByUserId,
            o.Notes,
            o.SyncId,
            o.CreatedAt,
            o.UpdatedAt,
            producedQuantity,
            scrappedQuantity,
            remaining,
            confirmationsCount,
            completedAt,
            closedAt);
    }
}
