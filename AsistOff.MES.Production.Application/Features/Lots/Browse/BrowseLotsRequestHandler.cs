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
        if (!string.IsNullOrWhiteSpace(request.Search))
            predicate = predicate.And(x => x.Code.Contains(request.Search));
        if (request.ProductId.HasValue)
            predicate = predicate.And(x => x.ProductId == request.ProductId.Value);
        if (request.Status.HasValue)
            predicate = predicate.And(x => x.Status == request.Status.Value);
        if (request.ExpiryFrom.HasValue)
            predicate = predicate.And(x => x.ExpiryDate != null && x.ExpiryDate >= request.ExpiryFrom.Value);
        if (request.ExpiryTo.HasValue)
            predicate = predicate.And(x => x.ExpiryDate != null && x.ExpiryDate <= request.ExpiryTo.Value);

        IPagedRequest paging = request;
        int? effectivePageSize = request.PageSize;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Capped typeahead: first page ordered by code, at most 20 rows.
            // MaxPageSize (200) still caps the regular browse path via validation.
            effectivePageSize = Math.Min(request.PageSize ?? BrowseLotsRequest.MaxLookupRows, BrowseLotsRequest.MaxLookupRows);
            paging = new LookupPaging(request, effectivePageSize);
        }

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<Lot>(predicate, paging);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedLotsResponse(
            items.Select(LotMappings.Map).ToList(), totalCount, effectivePageSize);
    }

    /// <summary>
    /// Wraps the incoming request for <c>Search</c> typeahead: caps the page to
    /// <c>MaxLookupRows</c> and defaults the sort to code ascending so shopfloor
    /// lookups stay bounded and deterministic.
    /// </summary>
    private sealed class LookupPaging(BrowseLotsRequest inner, int? pageSize) : IPagedRequest
    {
        public List<string> RawSort { get; set; } = inner.RawSort.Count == 0 ? ["Code"] : inner.RawSort;
        public IReadOnlyCollection<string> SupportedSortFields => inner.SupportedSortFields;
        public int? PageNumber => 1;
        public int? PageSize => pageSize;
        public int? MaxPageSize => inner.MaxPageSize;
    }
}
