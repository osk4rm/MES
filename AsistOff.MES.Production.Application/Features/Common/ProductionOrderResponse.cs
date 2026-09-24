using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Common;

public record ProductionOrderResponse(
    Guid Id,
    string Code,
    Guid ProductId,
    Guid RecipeId,
    Guid RecipeVersionId,
    decimal PlannedQuantity,
    Guid? MeasureUnitId,
    int Priority,
    DateTime? DueDate,
    ProductionOrderStatus Status,
    DateTime? ReleasedAt,
    Guid? ReleasedByUserId,
    string? Notes,
    string? SyncId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
