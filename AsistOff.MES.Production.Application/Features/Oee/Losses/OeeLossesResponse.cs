namespace AsistOff.MES.Production.Application.Features.Oee.Losses;

/// <summary>
/// One downtime Pareto row: closed-event overlap minutes for a single reason
/// code and its share of the total downtime in the window.
/// </summary>
public sealed record DowntimeParetoEntry(
    Guid ReasonCodeId,
    string? Code,
    string? DisplayName,
    double Minutes,
    double Share);

/// <summary>
/// One scrap Pareto row: total scrap quantity for a single reason code and
/// its share of the total scrap in the window.
/// </summary>
public sealed record ScrapParetoEntry(
    Guid ReasonCodeId,
    string? Code,
    string? DisplayName,
    decimal Quantity,
    double Share);

/// <summary>
/// Loss Pareto contract: echoed inputs, window totals and both Pareto lists
/// in descending value order. Pareto values always sum to their totals;
/// <c>Code</c> and <c>DisplayName</c> resolve from the caller-tenant reason
/// code dictionary and stay null when the code is missing there.
/// </summary>
public sealed record OeeLossesResponse(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc,
    double TotalDowntimeMinutes,
    IReadOnlyList<DowntimeParetoEntry> DowntimePareto,
    decimal TotalScrapQuantity,
    IReadOnlyList<ScrapParetoEntry> ScrapPareto);
