using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Recipes.Update;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateRecipeRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? PrimaryProductId,
    string? SyncId) : ITenantRequest;
