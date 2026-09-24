using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Create;

public record CreateMaintenanceWorkOrderRequest(
    string Code,
    string Title,
    string? Description,
    Guid MachineId,
    MaintenanceWorkOrderPriority Priority) : ITenantRequest<MaintenanceWorkOrderResponse>;
