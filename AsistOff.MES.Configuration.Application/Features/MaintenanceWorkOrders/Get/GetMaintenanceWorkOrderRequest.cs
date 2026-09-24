using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Get;

public record GetMaintenanceWorkOrderRequest(Guid Id) : ITenantRequest<MaintenanceWorkOrderResponse>;
