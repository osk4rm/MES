using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Oee.Losses;

/// <summary>
/// Read-only OEE loss Pareto over a caller-supplied UTC window: downtime
/// minutes by reason code (closed events only) and scrap quantities by
/// reason code. Reason codes resolve from the caller-tenant dictionary;
/// no new tables and no ideal cycle time (losses are raw totals).
/// </summary>
public record GetOeeLossesRequest(
    Guid MachineId,
    DateTime FromUtc,
    DateTime ToUtc) : ITenantRequest<OeeLossesResponse>;
