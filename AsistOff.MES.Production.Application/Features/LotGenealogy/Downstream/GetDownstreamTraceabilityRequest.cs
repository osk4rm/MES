using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Downstream;

/// <summary>
/// Where-used query: transitive closure of produced (finished good) lots for
/// a material lot, up to <see cref="MaxDepth"/> levels (1..10, default 5).
/// </summary>
public record GetDownstreamTraceabilityRequest(Guid LotId, int? MaxDepth = null)
    : ITenantRequest<LotTraceabilityResponse>;
