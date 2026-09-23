using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders;

internal static class ProductionOrderMappers
{
    public static ProductionOrderResponse Map(ProductionOrder o) => new(
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
        o.UpdatedAt);
}
