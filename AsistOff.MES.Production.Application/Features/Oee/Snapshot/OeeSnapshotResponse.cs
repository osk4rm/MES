namespace AsistOff.MES.Production.Application.Features.Oee.Snapshot;

/// <summary>
/// OEE snapshot contract: echoed inputs, the four nullable factors
/// (null when they cannot be computed — never zeros), per-factor computed
/// flags and the component totals the factors were derived from.
/// </summary>
public sealed record OeeSnapshotResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal IdealCycleTimeSeconds,
    double? Availability,
    double? Performance,
    double? Quality,
    double? Oee,
    bool AvailabilityComputed,
    bool PerformanceComputed,
    bool QualityComputed,
    double PlannedProductionTimeMinutes,
    double RunTimeMinutes,
    double DowntimeMinutes,
    decimal TotalCount,
    decimal GoodCount,
    decimal ScrapCount);
