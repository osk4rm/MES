using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Start;

public record StartMaintenanceWorkOrderRequest(Guid Id) : ITenantRequest<MaintenanceWorkOrderResponse>;
