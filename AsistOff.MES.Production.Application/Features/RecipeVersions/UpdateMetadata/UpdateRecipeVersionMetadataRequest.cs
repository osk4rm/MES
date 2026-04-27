using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.UpdateMetadata;

public record UpdateRecipeVersionMetadataRequest(
    Guid VersionId,
    string? ChangeNotes,
    DateTime? ValidFrom,
    DateTime? ValidTo) : ITenantRequest;
