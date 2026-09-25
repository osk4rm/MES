using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Recipes.Create;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record CreateRecipeRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? PrimaryProductId,
    string? SyncId) : ITenantRequest<RecipeResponse>;
