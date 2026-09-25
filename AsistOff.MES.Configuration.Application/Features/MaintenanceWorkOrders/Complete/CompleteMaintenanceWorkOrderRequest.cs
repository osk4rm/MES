using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Complete;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CompleteMaintenanceWorkOrderRequest(Guid Id, string? ResolutionNotes)
    : ITenantRequest<MaintenanceWorkOrderResponse>;
