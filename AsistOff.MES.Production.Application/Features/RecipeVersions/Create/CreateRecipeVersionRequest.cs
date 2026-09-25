using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Create;

/// <summary>
/// Creates a new empty Draft version for the given recipe. Use the Clone endpoint
/// to copy operations from a source version.
/// </summary>
[RequirePermission(RbacDefaults.ProductionWrite)]
public record CreateRecipeVersionRequest(
    Guid RecipeId,
    string? ChangeNotes,
    DateTime? ValidFrom,
    DateTime? ValidTo) : ITenantRequest<RecipeVersionDetailResponse>;
