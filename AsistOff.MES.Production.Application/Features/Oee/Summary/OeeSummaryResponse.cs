namespace AsistOff.MES.Production.Application.Features.Oee.Summary;

/// <summary>
/// OEE summary contract (slices 1+2+3): echoed inputs, raw confirmation counts,
/// the Quality factor (<c>GoodCount / TotalCount</c>, rounded to 4 decimals;
/// null when <c>TotalCount</c> is zero — never zero), the Availability
/// factor (<c>RunTimeMinutes / PlannedTimeMinutes</c>, rounded to 4 decimals;
/// null when <c>PlannedTimeMinutes</c> is zero — never zero), the Performance
/// factor (<c>TotalCount * IdealCycleTimeSeconds / RunTimeMinutes</c>, rounded
/// to 4 decimals and clamped at 1; null when the ideal cycle time is unknown,
/// when <c>RunTimeMinutes</c> is zero or when <c>TotalCount</c> is zero) and
/// the composite OEE (<c>Availability * Performance * Quality</c>, rounded to
/// 4 decimals; null when any factor is null).
/// Planned time is the overlap of the Work Center calendar Shifts with the
/// window; downtime sums closed DowntimeEvent overlap only (open events are
/// ignored); run time is planned minus downtime floored at zero.
/// <c>IdealCycleTimeSeconds</c> is the minimum positive
/// <c>OperationNode.RunTimePerUnitSeconds</c> across the recipe versions of
/// the confirmed Production Orders in the window (null when unresolvable);
/// over-cycle readings (raw performance above 1) are clamped at 1 because the
/// ideal time is defined as the fastest sustainable per-unit time.
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
    double? Availability,
    decimal? IdealCycleTimeSeconds,
    double? Performance,
    double? Oee);
