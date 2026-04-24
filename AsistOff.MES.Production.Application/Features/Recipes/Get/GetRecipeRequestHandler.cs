using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Recipes.Get;

internal sealed class GetRecipeRequestHandler(IRecipesRepository repository)
    : IRequestHandler<GetRecipeRequest, RecipeResponse>
{
    public async Task<RecipeResponse> Handle(GetRecipeRequest request, CancellationToken cancellationToken)
    {
        var recipe = await repository.GetWithVersionsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Recipe", request.Id);
        return ProductionMappers.Map(recipe);
    }
}
