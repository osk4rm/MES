using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Browse;

internal sealed class BrowseProductionOrdersRequestHandler(IProductionOrdersRepository repository)
    : IRequestHandler<BrowseProductionOrdersRequest, PagedResponse<ProductionOrderResponse>>
{
    public async Task<PagedResponse<ProductionOrderResponse>> Handle(BrowseProductionOrdersRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<ProductionOrder>(true);

        if (!string.IsNullOrWhiteSpace(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));
        if (request.Status.HasValue)
            predicate = predicate.And(x => x.Status == request.Status);
        if (request.ProductId.HasValue)
            predicate = predicate.And(x => x.ProductId == request.ProductId);
        if (request.RecipeId.HasValue)
            predicate = predicate.And(x => x.RecipeId == request.RecipeId);
        if (request.DueFrom.HasValue)
            predicate = predicate.And(x => x.DueDate >= request.DueFrom);
        if (request.DueTo.HasValue)
            predicate = predicate.And(x => x.DueDate <= request.DueTo);

        var total = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<ProductionOrder>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedProductionOrdersResponse(items.Select(ProductionOrderMappers.Map).ToList(), total, request.PageSize);
    }
}
