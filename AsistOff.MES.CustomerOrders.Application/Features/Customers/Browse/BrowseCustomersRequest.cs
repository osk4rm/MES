using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.CustomerOrders.Application.Features.Customers.Browse;

public class BrowseCustomersRequest : ITenantRequest<PagedResponse<CustomerResponse>>, IPagedRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? TaxId { get; set; }
    public bool? IsActive { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Code", "Name", "TaxId", "IsActive"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
