using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateMaintenanceWorkOrderRequest(
    string Code,
    string Title,
    string? Description,
    Guid MachineId,
    MaintenanceWorkOrderPriority Priority) : ITenantRequest<MaintenanceWorkOrderResponse>;
