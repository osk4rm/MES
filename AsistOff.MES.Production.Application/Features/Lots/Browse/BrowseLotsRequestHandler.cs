using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Lots.Browse;

internal sealed class BrowseLotsRequestHandler(ILotsRepository repository)
    : IRequestHandler<BrowseLotsRequest, PagedResponse<LotResponse>>
{
    public async Task<PagedResponse<LotResponse>> Handle(BrowseLotsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<Lot>(true);

        if (!string.IsNullOrWhiteSpace(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));
        if (request.ProductId.HasValue)
            predicate = predicate.And(x => x.ProductId == request.ProductId.Value);
        if (request.Status.HasValue)
            predicate = predicate.And(x => x.Status == request.Status.Value);
        if (request.ExpiryFrom.HasValue)
            predicate = predicate.And(x => x.ExpiryDate != null && x.ExpiryDate >= request.ExpiryFrom.Value);
        if (request.ExpiryTo.HasValue)
            predicate = predicate.And(x => x.ExpiryDate != null && x.ExpiryDate <= request.ExpiryTo.Value);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<Lot>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedLotsResponse(
            items.Select(LotMappings.Map).ToList(), totalCount, request.PageSize);
    }
}
