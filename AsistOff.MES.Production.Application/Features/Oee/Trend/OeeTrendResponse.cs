using AsistOff.MES.Production.Application.Features.Oee.Snapshot;

namespace AsistOff.MES.Production.Application.Features.Oee.Trend;

/// <summary>
/// OEE trend contract: echoed inputs with the canonical bucket name plus one
/// <see cref="OeeSnapshotResponse"/> per bucket in ascending time order.
/// Each entry matches the (1/3) snapshot for that bucket window.
/// </summary>
public sealed record OeeTrendResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    string Bucket,
    decimal IdealCycleTimeSeconds,
    IReadOnlyList<OeeSnapshotResponse> Entries);
