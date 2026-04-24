using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Clone;

/// <summary>
/// Clones an existing version (any status) into a new Draft version of the
/// same recipe. Deep-copies operations, dependencies, BOM items, outputs and
/// resource requirements with freshly generated IDs.
/// </summary>
public record CloneRecipeVersionRequest(
    Guid SourceVersionId,
    string? ChangeNotes,
    DateTime? ValidFrom,
    DateTime? ValidTo) : ITenantRequest<RecipeVersionDetailResponse>;
