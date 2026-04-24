using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Recipes.Browse;

internal sealed class BrowseRecipesRequestHandler(IRecipesRepository repository)
    : IRequestHandler<BrowseRecipesRequest, PagedResponse<RecipeResponse>>
{
    public async Task<PagedResponse<RecipeResponse>> Handle(BrowseRecipesRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<Recipe>(true);

        if (!string.IsNullOrWhiteSpace(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));
        if (!string.IsNullOrWhiteSpace(request.Name))
            predicate = predicate.And(x => x.Name.Contains(request.Name));
        if (request.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == request.IsActive);
        if (request.PrimaryProductId.HasValue)
            predicate = predicate.And(x => x.PrimaryProductId == request.PrimaryProductId);

        var total = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<Recipe>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedRecipesResponse(items.Select(ProductionMappers.Map).ToList(), total, request.PageSize);
    }
}
