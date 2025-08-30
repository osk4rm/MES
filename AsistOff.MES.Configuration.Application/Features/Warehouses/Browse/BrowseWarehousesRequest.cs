using AsistOff.MES.Configuration.Application.Features.Warehouses.Common.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;

public class BrowseWarehousesRequest : ITenantRequest<PagedResponse<WarehouseResponse>>, IPagedRequest
{
    public string? Name { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Name"]; 
    public int? PageNumber => 1;
    public int? PageSize => 20;
    public int? MaxPageSize => 100;
}
