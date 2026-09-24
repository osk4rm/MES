using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Complete;

public record CompleteMaintenanceWorkOrderRequest(Guid Id, string? ResolutionNotes)
    : ITenantRequest<MaintenanceWorkOrderResponse>;
