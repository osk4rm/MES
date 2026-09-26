using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.Lots.Browse;

public class BrowseLotsRequest
    : ITenantRequest<PagedResponse<LotResponse>>, IPagedRequest
{
    public string? Code { get; set; }
    /// <summary>
    /// Shopfloor typeahead: substring match on <c>Code</c>. When set, the
    /// handler orders by code and caps the page to at most 20 rows
    /// (issue #274).
    /// </summary>
    public string? Search { get; set; }
    public Guid? ProductId { get; set; }
    public LotStatus? Status { get; set; }
    public DateTime? ExpiryFrom { get; set; }
    public DateTime? ExpiryTo { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Code", "ProductId", "Status", "ExpiryDate"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
