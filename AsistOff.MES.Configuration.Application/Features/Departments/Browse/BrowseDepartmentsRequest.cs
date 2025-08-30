using AsistOff.MES.Configuration.Application.Features.Departments.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Departments.Browse;

public class BrowseDepartmentsRequest
    : ITenantRequest<PagedResponse<DepartmentResponse>>, IPagedRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Name", "Code"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}