using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Recipes.Delete;

public record DeleteRecipeRequest(Guid Id) : ITenantRequest;
