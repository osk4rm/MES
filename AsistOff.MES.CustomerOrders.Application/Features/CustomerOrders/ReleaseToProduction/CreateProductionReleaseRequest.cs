using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.ReleaseToProduction;

public record CreateProductionReleaseRequest(
    Guid CustomerOrderLineId,
    Guid RecipeId,
    Guid? RecipeVersionId,
    decimal Quantity,
    DateTime? PlannedStartDate,
    DateTime? PlannedDueDate,
    string? Notes) : ITenantRequest<CustomerOrderResponse>;
