using AsistOff.MES.Configuration.Application.Features.Warehouses.Common.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;

public class BrowseWarehousesRequest : ITenantRequest<PagedResponse<WarehouseItemResponse>>, IPagedRequest
{
    public string? Name { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Name"]; 
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
