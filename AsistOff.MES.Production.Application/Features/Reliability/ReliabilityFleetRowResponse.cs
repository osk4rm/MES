namespace AsistOff.MES.Production.Application.Features.Reliability;

/// <summary>
/// One fleet comparison row: the Work Center identity plus failure/repair
/// counts, the window/uptime/downtime totals and the nullable MTBF/MTTR/
/// average-repair KPIs (null when they cannot be computed — never zeros).
/// Matches the snapshot computation for the same window.
/// </summary>
public sealed record ReliabilityFleetRowResponse(
    Guid MachineId,
    string MachineCode,
    string MachineName,
    Guid? DepartmentId,
    int FailureCount,
    int RepairCount,
    double WindowMinutes,
    double UptimeMinutes,
    double TotalDowntimeMinutes,
    double? MtbfMinutes,
    double? MttrMinutes,
    double? AvgRepairMinutes);
