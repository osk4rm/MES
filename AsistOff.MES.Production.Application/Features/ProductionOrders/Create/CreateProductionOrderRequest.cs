using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Create;

[RequirePermission(RbacDefaults.ProductionWrite)]
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
