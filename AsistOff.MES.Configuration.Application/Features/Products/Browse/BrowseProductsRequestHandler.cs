using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Products.Browse;

internal sealed class BrowseProductsRequestHandler(
    IProductsRepository productsRepository)
    : IRequestHandler<BrowseProductsRequest, PagedResponse<ProductResponse>>
{
    public async Task<PagedResponse<ProductResponse>> Handle(BrowseProductsRequest request, CancellationToken cancellationToken)
    {
        var filter = BuildPredicate(request);

        IPagedRequest paging = request;
        int? effectivePageSize = request.PageSize;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Capped typeahead: first page ordered by code, at most 20 rows.
            // MaxPageSize (100) still caps the regular browse path via validation.
            effectivePageSize = Math.Min(request.PageSize ?? BrowseProductsRequest.MaxLookupRows, BrowseProductsRequest.MaxLookupRows);
            paging = new LookupPaging(request, effectivePageSize);
        }

        var totalCount = await productsRepository.CountAsync(filter, cancellationToken);
        var paginator = new Paginator<Product>(filter, paging);

        var products = await productsRepository.BrowseAsync(paginator, cancellationToken);

        var items = products.Select(x => new ProductResponse(
            x.Id,
            x.SyncId,
            x.Code,
            x.Name,
            x.Description,
            x.Ean,
            x.Barcode,
            x.ScanBy,
            x.IsActive,
            x.ProductGroupId.HasValue && x.ProductGroup != null
                ? new ProductGroupShortResponse(x.ProductGroupId.Value, x.ProductGroup.Code, x.ProductGroup.Name)
                : null,
            x.ProductMeasureUnits.FirstOrDefault(mu => mu.IsDefault)?.MeasureUnit is { } mu
                ? new MeasureUnitShortResponse(mu.Id, mu.Name, mu.Symbol)
                : null
        )).ToList();

        return new PagedProductsResponse(items, totalCount, effectivePageSize);
    }

    /// <summary>
    /// Wraps the incoming request for <c>Search</c> typeahead: caps the page to
    /// <c>MaxLookupRows</c> and defaults the sort to code ascending so shopfloor
    /// lookups stay bounded and deterministic.
    /// </summary>
    private sealed class LookupPaging(BrowseProductsRequest inner, int? pageSize) : IPagedRequest
    {
        public List<string> RawSort { get; set; } = inner.RawSort.Count == 0 ? ["Code"] : inner.RawSort;
        public IReadOnlyCollection<string> SupportedSortFields => inner.SupportedSortFields;
        public int? PageNumber => 1;
        public int? PageSize => pageSize;
        public int? MaxPageSize => inner.MaxPageSize;
    }

    private ExpressionStarter<Product> BuildPredicate(BrowseProductsRequest request)
    {
        var predicate = PredicateBuilder.New<Product>(true);

        if (!string.IsNullOrWhiteSpace(request.Name))
            predicate = predicate.And(x => x.Name.Contains(request.Name));

        if (!string.IsNullOrEmpty(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));

        if (!string.IsNullOrWhiteSpace(request.Search))
            predicate = predicate.And(x => x.Code.Contains(request.Search));

        if (request.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == request.IsActive);

        if (request.GroupId.HasValue)
            predicate = predicate.And(x => x.ProductGroupId == request.GroupId);

        if (!string.IsNullOrWhiteSpace(request.Ean))
            predicate = predicate.And(x => x.Ean == request.Ean);

        if (!string.IsNullOrWhiteSpace(request.Barcode))
            predicate = predicate.And(x => x.Barcode == request.Barcode);

        if (request.ScanBy.HasValue)
            predicate = predicate.And(x => x.ScanBy == request.ScanBy);

        if (request.MeasureUnitId.HasValue)
            predicate = predicate.And(x => x.ProductMeasureUnits.Any(mu => mu.MeasureUnitId == request.MeasureUnitId));

        return predicate;
    }
}