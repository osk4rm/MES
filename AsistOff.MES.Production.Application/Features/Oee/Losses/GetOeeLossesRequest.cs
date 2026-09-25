using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Oee.Losses;

/// <summary>
/// Read-only per-Work Center loss Pareto over a caller-supplied UTC window:
/// downtime minutes from closed <c>DowntimeEvent</c> overlap grouped by
/// reason code, and scrap quantities from <c>ScrapEvent</c> rows grouped by
/// reason code. No new tables and no migration.
/// </summary>
public record GetOeeLossesRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc) : ITenantRequest<OeeLossesResponse>;
