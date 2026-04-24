using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Delete;

public record DeleteRecipeVersionRequest(Guid VersionId) : ITenantRequest;
