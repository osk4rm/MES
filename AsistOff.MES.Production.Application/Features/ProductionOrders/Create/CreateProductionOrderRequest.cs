using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Create;

public record CreateProductionOrderRequest(
    string Code,
    Guid ProductId,
    Guid RecipeId,
    Guid RecipeVersionId,
    decimal PlannedQuantity,
    Guid? MeasureUnitId,
    int Priority,
    DateTime? DueDate,
    string? Notes,
    string? SyncId) : ITenantRequest<ProductionOrderResponse>;
