using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.Recipes.Create;

public record CreateRecipeRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? PrimaryProductId,
    string? SyncId) : ITenantRequest<RecipeResponse>;
