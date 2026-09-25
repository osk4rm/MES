namespace AsistOff.MES.Production.Application.Features.Reliability.Trend;

/// <summary>
/// Reliability trend contract: echoed inputs with the bucket granularity
/// normalized to <c>Day</c> or <c>Week</c>, plus one
/// <see cref="ReliabilitySnapshotResponse"/> per bucket in ascending time
/// order.
/// </summary>
public sealed record ReliabilityTrendResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    string Bucket,
    IReadOnlyList<ReliabilitySnapshotResponse> Buckets);
