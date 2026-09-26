using AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Products.Browse;

public class BrowseProductsRequest
    : ITenantRequest<PagedResponse<ProductResponse>>, IPagedRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    /// <summary>
    /// Shopfloor typeahead fragment matched against <c>Code</c>. When set, the
    /// handler orders by code and caps the page to <see cref="MaxLookupRows"/>
    /// matches so the lookup stays bounded.
    /// </summary>
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public Guid? GroupId { get; set; }
    public string? Ean { get; set; }
    public string? Barcode { get; set; }
    public Guid? MeasureUnitId { get; set; }
    public ScanBy? ScanBy { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Name", "Code", "IsActive", "ScanBy"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;

    /// <summary>Upper bound for <c>Search</c> typeahead pages (ordered by code).</summary>
    public const int MaxLookupRows = 20;
}