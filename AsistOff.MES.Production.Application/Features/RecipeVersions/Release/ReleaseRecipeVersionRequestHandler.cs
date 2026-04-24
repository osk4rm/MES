using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.RecipeVersions.Release;

internal sealed class ReleaseRecipeVersionRequestHandler(
    IRecipeVersionsRepository versionsRepository,
    IRecipesRepository recipesRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReleaseRecipeVersionRequest>
{
    public async Task Handle(ReleaseRecipeVersionRequest request, CancellationToken cancellationToken)
    {
        var version = await versionsRepository.GetFullAsync(request.VersionId, cancellationToken)
            ?? throw new NotFoundException("RecipeVersion", request.VersionId);

        if (version.Status != RecipeVersionStatus.Draft)
            throw new ConflictException("Only Draft versions can be released.");

        if (version.Operations.Count == 0)
            throw new ValidationException(nameof(version.Operations), "A recipe version must contain at least one operation before it can be released.");

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
