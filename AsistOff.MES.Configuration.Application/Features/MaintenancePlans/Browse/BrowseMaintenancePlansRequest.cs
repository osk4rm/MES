using AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.MaintenancePlans.Browse;

public class BrowseMaintenancePlansRequest
    : ITenantRequest<PagedResponse<MaintenancePlanResponse>>, IPagedRequest
{
    public Guid? MachineId { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? DueBefore { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } =
        ["Code", "Name", "NextDueAt", "IsActive"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
