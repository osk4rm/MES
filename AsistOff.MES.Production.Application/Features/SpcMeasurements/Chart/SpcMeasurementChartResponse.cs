namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Chart;

public record SpcMeasurementChartPoint(
    Guid Id,
    decimal Value,
    DateTime MeasuredAt,
    bool IsOutOfControl,
    bool IsOutOfSpec);

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
    int OutOfSpecCount);
