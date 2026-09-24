using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Cancel;

public record CancelMaintenanceWorkOrderRequest(Guid Id) : ITenantRequest<MaintenanceWorkOrderResponse>;
