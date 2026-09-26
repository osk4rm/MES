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
    /// Shopfloor typeahead: substring match on <c>Code</c>. When set, the
    /// handler orders by code and caps the page to at most 20 rows
    /// (issue #274).
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
}