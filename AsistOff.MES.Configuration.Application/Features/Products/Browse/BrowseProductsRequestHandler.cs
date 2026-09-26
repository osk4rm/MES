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
    /// <summary>Upper bound for <c>Search</c> typeahead pages (issue #274).</summary>
    internal const int TypeaheadMaxRows = 20;

    public async Task<PagedResponse<ProductResponse>> Handle(BrowseProductsRequest request, CancellationToken cancellationToken)
    {
        var filter = BuildPredicate(request);
        var totalCount = await productsRepository.CountAsync(filter, cancellationToken);
        var paginator = string.IsNullOrWhiteSpace(request.Search)
            ? new Paginator<Product>(filter, request)
            : new Paginator<Product>(filter, CappedTypeahead(request));

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

        var pageSize = string.IsNullOrWhiteSpace(request.Search)
            ? request.PageSize
            : CappedTypeahead(request).PageSize;
        return new PagedProductsResponse(items, totalCount, pageSize);
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

    /// <summary>
    /// Typeahead paging: first page ordered by code, capped at
    /// <see cref="TypeaheadMaxRows"/> rows and at <c>MaxPageSize</c>, so a
    /// shopfloor lookup can never pull an unbounded page (issue #274).
    /// </summary>
    internal static TypeaheadPaging CappedTypeahead(BrowseProductsRequest request)
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