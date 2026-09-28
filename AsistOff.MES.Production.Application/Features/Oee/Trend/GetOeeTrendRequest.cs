using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Oee.Trend;

/// <summary>
/// Read-only per-Work Center OEE trend over a caller-supplied UTC window.
/// The window is partitioned into calendar-aligned <c>Day</c> (UTC midnight)
/// or <c>Week</c> (Monday 00:00 UTC) buckets and each bucket carries the
/// (1/3) snapshot for that sub-window; buckets without planned time carry
/// null factors, not zeros. No new tables; the ideal cycle time is optional —
/// when omitted it is resolved from the confirmed orders' operations
/// (routing master data, same rule as the summary endpoint). An explicit
/// positive value wins; a null ideal with no resolvable routing data is a
/// 400 naming <c>IdealCycleTimeSeconds</c>.
/// </summary>
public record GetOeeTrendRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal? IdealCycleTimeSeconds,
    string? Bucket) : ITenantRequest<OeeTrendResponse>;
