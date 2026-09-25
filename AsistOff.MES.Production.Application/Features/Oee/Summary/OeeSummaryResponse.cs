namespace AsistOff.MES.Production.Application.Features.Oee.Summary;

/// <summary>
/// OEE summary contract (slices 1+2): echoed inputs, raw confirmation counts,
/// the Quality factor (<c>GoodCount / TotalCount</c>, rounded to 4 decimals;
/// null when <c>TotalCount</c> is zero — never zero) and the Availability
/// factor (<c>RunTimeMinutes / PlannedTimeMinutes</c>, rounded to 4 decimals;
/// null when <c>PlannedTimeMinutes</c> is zero — never zero).
/// Planned time is the overlap of the Work Center calendar Shifts with the
/// window; downtime sums closed DowntimeEvent overlap only (open events are
/// ignored); run time is planned minus downtime floored at zero.
/// </summary>
public sealed record OeeSummaryResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal GoodCount,
    decimal ScrapCount,
    decimal TotalCount,
    double? Quality,
    double PlannedTimeMinutes,
    double RunTimeMinutes,
    double DowntimeMinutes,
    double? Availability);
