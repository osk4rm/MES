using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Create;

/// <summary>
/// Creates a new empty Draft version for the given recipe. Use the Clone endpoint
/// to copy operations from a source version.
/// </summary>
public record CreateRecipeVersionRequest(
    Guid RecipeId,
    string? ChangeNotes,
    DateTime? ValidFrom,
    DateTime? ValidTo) : ITenantRequest<RecipeVersionDetailResponse>;
