using AsistOff.MES.Configuration.Application.Features.Machines.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Machines.Browse;

public class BrowseMachinesRequest
    : ITenantRequest<PagedResponse<MachineResponse>>, IPagedRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public bool? IsActive { get; set; }
    public Guid? DepartmentId { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Name", "Code", "IsActive"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
