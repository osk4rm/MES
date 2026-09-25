using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Reliability;

/// <summary>
/// Read-only per-Work Center MTBF/MTTR snapshot over a caller-supplied UTC
/// window. Computed at read time from closed <c>DowntimeEvent</c> overlap
/// (failures) and Done <c>MaintenanceWorkOrder</c> rows (repairs). No new
/// tables and no migration.
/// </summary>
public record GetReliabilitySnapshotRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc) : ITenantRequest<ReliabilitySnapshotResponse>;
