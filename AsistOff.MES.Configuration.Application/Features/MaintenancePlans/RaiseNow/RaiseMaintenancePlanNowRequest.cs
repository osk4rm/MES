using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.RaiseNow;

/// <summary>
/// Manually raise one Open work order from a single preventive plan.
/// Skips due-date checks (operator forces it now) but keeps the idempotency
/// guard: when a live Open/InProgress order already exists for the plan the
/// existing order is returned instead of creating a duplicate.
/// </summary>
[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record RaiseMaintenancePlanNowRequest(Guid Id)
    : ITenantRequest<MaintenanceWorkOrderResponse>;
