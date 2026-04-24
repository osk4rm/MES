using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Get;

public record GetRecipeVersionRequest(Guid VersionId) : ITenantRequest<RecipeVersionDetailResponse>;
