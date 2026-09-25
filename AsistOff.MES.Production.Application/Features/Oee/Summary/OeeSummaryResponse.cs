namespace AsistOff.MES.Production.Application.Features.Oee.Summary;

/// <summary>
/// OEE summary contract (slice 1): echoed inputs, raw confirmation counts
/// and the Quality factor (<c>GoodCount / TotalCount</c>, rounded to 4
/// decimals; null when <c>TotalCount</c> is zero — never zero).
/// </summary>
public sealed record OeeSummaryResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal GoodCount,
    decimal ScrapCount,
    decimal TotalCount,
    double? Quality);
