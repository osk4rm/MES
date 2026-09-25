using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Get;

public record GetLotGenealogyEdgeRequest(Guid Id) : ITenantRequest<LotGenealogyEdgeResponse>;
