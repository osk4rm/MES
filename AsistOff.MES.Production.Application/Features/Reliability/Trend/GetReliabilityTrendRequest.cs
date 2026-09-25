using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Reliability.Trend;

/// <summary>
/// Read-only per-Work Center MTBF/MTTR trend over a caller-supplied UTC
/// window. The window is partitioned into calendar-aligned <c>Day</c> (UTC
/// midnight) or <c>Week</c> (Monday 00:00 UTC) buckets and each bucket
/// carries the snapshot-equivalent computation for that sub-window (closed
/// downtime overlap only, Done work orders only, null KPIs when not
/// computable). No new tables and no migration.
/// </summary>
public record GetReliabilityTrendRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    string? Bucket) : ITenantRequest<ReliabilityTrendResponse>;
