using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Recipes.Update;

public record UpdateRecipeRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? PrimaryProductId,
    string? SyncId) : ITenantRequest;
