using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Delete;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record DeleteProductionConfirmationRequest(Guid Id) : ITenantRequest;
