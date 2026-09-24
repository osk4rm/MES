using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Oee.Trend;

/// <summary>
/// Read-only per-Work Center OEE trend over a caller-supplied UTC window,
/// partitioned into <c>Day</c> (UTC midnight boundaries) or <c>Week</c>
/// (Monday 00:00 UTC boundaries) buckets. Each bucket reuses the (1/3)
/// snapshot math; empty buckets carry null factors, never zeros.
/// No new tables; ideal cycle time stays a query parameter.
/// </summary>
public record GetOeeTrendRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal IdealCycleTimeSeconds,
    string Bucket) : ITenantRequest<OeeTrendResponse>;
