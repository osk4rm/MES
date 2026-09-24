namespace AsistOff.MES.Production.Application.Features.Reliability;

/// <summary>
/// Reliability snapshot contract: echoed inputs, failure/repair counts, the
/// window/uptime/downtime totals and the nullable MTBF/MTTR/average-repair
/// KPIs (null when they cannot be computed — never zeros).
/// </summary>
public sealed record ReliabilitySnapshotResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    int FailureCount,
    int RepairCount,
    double WindowMinutes,
    double UptimeMinutes,
    double TotalDowntimeMinutes,
    double? MtbfMinutes,
    double? MttrMinutes,
    double? AvgRepairMinutes);
