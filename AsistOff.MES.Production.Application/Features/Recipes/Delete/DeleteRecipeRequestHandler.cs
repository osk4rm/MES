using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Recipes.Delete;

internal sealed class DeleteRecipeRequestHandler(IRecipesRepository repository)
    : IRequestHandler<DeleteRecipeRequest>
{
    public async Task Handle(DeleteRecipeRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetWithVersionsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Recipe", request.Id);

        if (entity.Versions.Any(v => v.Status == RecipeVersionStatus.Released))
        {
            throw new ConflictException(
                "Recipe has at least one released version and cannot be deleted. Archive the versions instead.");
        }

        await repository.DeleteAsync(entity.Id, cancellationToken);
    }
}
