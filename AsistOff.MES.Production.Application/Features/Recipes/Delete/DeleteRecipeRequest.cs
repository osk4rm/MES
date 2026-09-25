using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Recipes.Delete;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record DeleteRecipeRequest(Guid Id) : ITenantRequest;
