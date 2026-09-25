using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Delete;

public record DeleteLotGenealogyEdgeRequest(Guid Id) : ITenantRequest;
