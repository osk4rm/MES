using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Browse;

internal sealed class BrowseProductionOrdersRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IProductionConfirmationsRepository confirmationsRepository)
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

        var total = await ordersRepository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<ProductionOrder>(predicate, request);
        var items = await ordersRepository.BrowseAsync(paginator, cancellationToken);

        var ids = items.Select(x => x.Id).ToList();
        var totals = await confirmationsRepository.GetTotalsForOrdersAsync(ids, cancellationToken);

        var mapped = items.Select(order =>
        {
            totals.TryGetValue(order.Id, out var t);
            return ProductionOrderMappers.Map(order, t.ProducedQuantity, t.ScrappedQuantity, t.ConfirmationsCount);
        }).ToList();

        return new PagedProductionOrdersResponse(mapped, total, request.PageSize);
    }
}
