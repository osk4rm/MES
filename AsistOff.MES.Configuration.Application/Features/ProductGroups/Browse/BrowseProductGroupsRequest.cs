using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Browse;

public class BrowseProductGroupsRequest
    : ITenantRequest<PagedResponse<ProductGroupResponse>>,
        IPagedRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public bool? IsActive { get; set; }
    public Guid? ParentId { get; set; }
    public IReadOnlyCollection<string> RawSort { get; } = [];
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Name", "Code", "IsActive"];
    public int? PageNumber => 1;
    public int? PageSize => 20;
    public int? MaxPageSize => 100;
}