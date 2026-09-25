using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Start;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record StartMaintenanceWorkOrderRequest(Guid Id) : ITenantRequest<MaintenanceWorkOrderResponse>;
