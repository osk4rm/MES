using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Delete;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record DeleteLotGenealogyEdgeRequest(Guid Id) : ITenantRequest;
