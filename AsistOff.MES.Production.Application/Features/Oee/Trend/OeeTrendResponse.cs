using AsistOff.MES.Production.Application.Features.Oee.Snapshot;

namespace AsistOff.MES.Production.Application.Features.Oee.Trend;

/// <summary>
/// OEE trend contract: echoed inputs with the bucket granularity normalized
/// to <c>Day</c> or <c>Week</c>, plus one <see cref="OeeSnapshotResponse"/>
/// per bucket in ascending time order.
/// </summary>
public sealed record OeeTrendResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal IdealCycleTimeSeconds,
    string Bucket,
    IReadOnlyList<OeeSnapshotResponse> Buckets);
