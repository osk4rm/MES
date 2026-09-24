namespace AsistOff.MES.Production.Application.Features.Oee.Losses;

/// <summary>
/// One downtime Pareto line: overlapped closed-event minutes for a reason
/// code and its share of total downtime (0-1, rounded to 4 decimals).
/// Sorted by minutes descending, then code ascending.
/// </summary>
public sealed record OeeDowntimeParetoEntry(
    Guid ReasonCodeId,
    string Code,
    string Name,
    double Minutes,
    double Share);

/// <summary>
/// One scrap Pareto line: total <c>ScrapEvent</c> quantity for a reason code
/// and its share of total scrap (0-1, rounded to 4 decimals).
/// Sorted by quantity descending, then code ascending.
/// </summary>
public sealed record OeeScrapParetoEntry(
    Guid ReasonCodeId,
    string Code,
    string Name,
    decimal Quantity,
    double Share);

/// <summary>
/// OEE loss Pareto contract: echoed inputs, window totals and the two Pareto
/// breakdowns. Downtime minutes sum to the (1/3) snapshot run-time loss;
/// scrap quantities sum to total <c>ScrapEvent</c> quantity in the window.
/// </summary>
public sealed record OeeLossesResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    double TotalDowntimeMinutes,
    decimal TotalScrapQuantity,
    IReadOnlyList<OeeDowntimeParetoEntry> DowntimePareto,
    IReadOnlyList<OeeScrapParetoEntry> ScrapPareto);
