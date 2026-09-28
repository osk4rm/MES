using AsistOff.MES.Production.Application.Features.Oee.Snapshot;

namespace AsistOff.MES.Production.Application.Features.Oee.Trend;

/// <summary>
/// OEE trend contract: echoed inputs with the bucket granularity normalized
/// to <c>Day</c> or <c>Week</c>, plus one <see cref="OeeSnapshotResponse"/>
/// per bucket in ascending time order.
/// <c>IdealCycleTimeSource</c> echoes where the effective ideal came from:
/// <c>caller</c> for an explicit query value, <c>routing</c> for the
/// routing-master-data resolution (additive field, shape otherwise unchanged).
/// </summary>
public sealed record OeeTrendResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal IdealCycleTimeSeconds,
    string Bucket,
    IReadOnlyList<OeeSnapshotResponse> Buckets,
    string IdealCycleTimeSource);
