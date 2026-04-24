using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.Recipes.Get;

public record GetRecipeRequest(Guid Id) : ITenantRequest<RecipeResponse>;
