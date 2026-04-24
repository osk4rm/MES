using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Release;

public record ReleaseRecipeVersionRequest(Guid VersionId) : ITenantRequest;
