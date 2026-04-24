using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Recipes.Create;

internal sealed class CreateRecipeRequestHandler(
    IRecipesRepository recipesRepository,
    IRecipeVersionsRepository versionsRepository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateRecipeRequest, RecipeResponse>
{
    public async Task<RecipeResponse> Handle(CreateRecipeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(nameof(request.Name), "Name is required");

        if (await recipesRepository.CodeExistsAsync(request.Code, null, cancellationToken))
            throw new ConflictException($"Recipe with code '{request.Code}' already exists.");

        var recipe = new Recipe
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            PrimaryProductId = request.PrimaryProductId,
            SyncId = request.SyncId
        };
        await recipesRepository.AddAsync(recipe, cancellationToken);

        // Every recipe is created together with its first Draft version so that
        // the user can start editing operations immediately.
        var firstVersion = new RecipeVersion
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            RecipeId = recipe.Id,
            VersionNumber = 1,
            Status = RecipeVersionStatus.Draft,
            CreatedAt = dateTimeProvider.UtcNow
        };
        await versionsRepository.AddAsync(firstVersion, cancellationToken);

        var full = await recipesRepository.GetWithVersionsAsync(recipe.Id, cancellationToken)!;
        return ProductionMappers.Map(full!);
    }
}
