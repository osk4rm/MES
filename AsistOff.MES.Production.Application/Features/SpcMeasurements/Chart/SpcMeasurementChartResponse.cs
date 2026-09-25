namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Chart;

public record SpcMeasurementChartPoint(
    Guid Id,
    decimal Value,
    DateTime MeasuredAt,
    bool IsOutOfControl,
    bool IsOutOfSpec,
    IReadOnlyCollection<int> ViolatedRules);

/// <summary>
/// Control chart evaluation with Western Electric rules 1-4 flags.
/// <c>IsOutOfControl</c> / <c>OutOfControlCount</c> stay rule-1-only
/// (backwards compatible); <c>ViolatedRules</c> per point plus the
/// <c>Rule2/3/4ViolationCount</c> totals carry rules 2-4.
/// </summary>
public record SpcMeasurementChartResponse(
    Guid CharacteristicId,
    decimal? NominalValue,
    decimal? LowerSpecLimit,
    decimal? UpperSpecLimit,
    decimal? LowerControlLimit,
    decimal? UpperControlLimit,
    IReadOnlyCollection<SpcMeasurementChartPoint> Points,
    int TotalCount,
    int OutOfControlCount,
    int OutOfSpecCount,
    int Rule2ViolationCount,
    int Rule3ViolationCount,
    int Rule4ViolationCount);
