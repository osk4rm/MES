using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Reliability;

/// <summary>
/// Read-only fleet comparison of MTBF/MTTR across active Work Centers over a
/// caller-supplied UTC window. One row per active caller-tenant machine,
/// optionally narrowed to a single department. Computed at read time from
/// closed <c>DowntimeEvent</c> overlap (failures) and Done
/// <c>MaintenanceWorkOrder</c> rows (repairs) with exactly the snapshot
/// semantics. No new tables and no migration.
/// </summary>
public record GetReliabilityFleetRequest(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? DepartmentId) : ITenantRequest<IReadOnlyList<ReliabilityFleetRowResponse>>;
