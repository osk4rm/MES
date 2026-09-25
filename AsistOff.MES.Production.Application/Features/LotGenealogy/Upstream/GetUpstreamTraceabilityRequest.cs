using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Upstream;

/// <summary>
/// Where-from query: transitive closure of consumed (component) lots for a
/// finished good lot, up to <see cref="MaxDepth"/> levels (1..10, default 5).
/// </summary>
public record GetUpstreamTraceabilityRequest(Guid LotId, int? MaxDepth = null)
    : ITenantRequest<LotTraceabilityResponse>;
