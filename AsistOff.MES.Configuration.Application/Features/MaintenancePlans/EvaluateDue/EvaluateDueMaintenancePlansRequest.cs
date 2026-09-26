using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.EvaluateDue;

/// <summary>
/// Evaluate due preventive plans and raise one Open work order per due plan.
/// Time plans are due when <c>NextDueAt &lt;= now</c>; Meter plans are due when
/// the supplied meter reading reaches <c>MeterIntervalValue</c>. Inactive plans
/// are never evaluated. Repeating the call without completing the raised order
/// does not create duplicates (idempotency guard on Open/InProgress orders).
/// </summary>
[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record EvaluateDueMaintenancePlansRequest(
    decimal? CurrentMeterReading = null,
    decimal? MeterReading = null,
    Dictionary<Guid, decimal>? MeterReadings = null)
    : ITenantRequest<IReadOnlyCollection<MaintenanceWorkOrderResponse>>;
