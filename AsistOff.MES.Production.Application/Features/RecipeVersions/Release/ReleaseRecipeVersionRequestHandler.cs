using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Release;

internal sealed class ReleaseRecipeVersionRequestHandler(
    IRecipeVersionsRepository versionsRepository,
    IRecipesRepository recipesRepository,
    IProductsRepository productsRepository,
    IWarehousesRepository warehousesRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReleaseRecipeVersionRequest>
{
    public async Task Handle(ReleaseRecipeVersionRequest request, CancellationToken cancellationToken)
    {
        var version = await versionsRepository.GetFullAsync(request.VersionId, cancellationToken)
            ?? throw new NotFoundException("RecipeVersion", request.VersionId);

        if (version.Status != RecipeVersionStatus.Draft)
            throw new ConflictException("Only Draft versions can be released.");

        // Server-side recheck of the release preflight rules (issue #388, R-7):
        // the checklist dialog cannot be bypassed from the API. Fail-state
        // rules surface as a structured 400; warn-state rules do not block.
        var productIds = version.Operations
            .SelectMany(o => o.BomItems.Select(b => b.ProductId).Concat(o.Outputs.Select(o => o.ProductId)))
            .ToHashSet();
        var productActiveById = new Dictionary<Guid, bool>(productIds.Count);
        foreach (var productId in productIds)
        {
            var product = await productsRepository.GetAsync(productId, cancellationToken);
            if (product is not null)
                productActiveById[productId] = product.IsActive;
        }

        var warehouses = await warehousesRepository.BrowseAsync(cancellationToken);
        var checks = RecipeReleasePreflight.Evaluate(
            version,
            productActiveById,
            warehouses.Select(w => w.Id).ToHashSet());

        var failures = checks.Where(c => c.State == PreflightState.Fail).ToList();
        if (failures.Count > 0)
            throw new ValidationException(
                failures.ToDictionary(f => f.Rule, f => new[] { f.Message }));

        var now = dateTimeProvider.UtcNow;

        // Demote any previously released version of the same recipe to Obsolete.
        var siblings = await versionsRepository.ListForRecipeAsync(version.RecipeId, cancellationToken);
        foreach (var sibling in siblings)
        {
            if (sibling.Id == version.Id) continue;
            if (sibling.Status == RecipeVersionStatus.Released)
            {
                sibling.Status = RecipeVersionStatus.Obsolete;
                sibling.UpdatedAt = now;
            }
        }

        version.Status = RecipeVersionStatus.Released;
        version.ReleasedAt = now;
        version.UpdatedAt = now;

        await versionsRepository.SaveChangesAsync(cancellationToken);

        // Point the recipe at the newly released version so production orders can use it.
        var recipe = await recipesRepository.GetAsync(version.RecipeId, cancellationToken)
            ?? throw new NotFoundException("Recipe", version.RecipeId);

        recipe.CurrentVersionId = version.Id;
        await recipesRepository.UpdateAsync(recipe, cancellationToken);
    }
}
