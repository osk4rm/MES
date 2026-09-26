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
    /// <summary>Upper bound for <c>Search</c> typeahead pages (issue #274).</summary>
    internal const int TypeaheadMaxRows = 20;

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

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = string.IsNullOrWhiteSpace(request.Search)
            ? new Paginator<Lot>(predicate, request)
            : new Paginator<Lot>(predicate, CappedTypeahead(request));
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var pageSize = string.IsNullOrWhiteSpace(request.Search)
            ? request.PageSize
            : CappedTypeahead(request).PageSize;
        return new PagedLotsResponse(
            items.Select(LotMappings.Map).ToList(), totalCount, pageSize);
    }

    /// <summary>
    /// Typeahead paging: first page ordered by code, capped at
    /// <see cref="TypeaheadMaxRows"/> rows and at <c>MaxPageSize</c>, so a
    /// shopfloor lookup can never pull an unbounded page (issue #274).
    /// </summary>
    internal static TypeaheadPaging CappedTypeahead(BrowseLotsRequest request)
    {
        var size = Math.Min(request.PageSize ?? TypeaheadMaxRows, TypeaheadMaxRows);
        if (request.MaxPageSize.HasValue)
            size = Math.Min(size, request.MaxPageSize.Value);
        return new TypeaheadPaging(request.PageNumber ?? 1, Math.Max(size, 1));
    }

    internal sealed class TypeaheadPaging(int pageNumber, int pageSize) : IPagedRequest
    {
        public int? PageNumber { get; } = pageNumber;
        public int? PageSize { get; } = pageSize;
        public int? MaxPageSize => TypeaheadMaxRows;
        public List<string> RawSort { get; set; } = ["Code"];
        public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Code"];
    }
}
