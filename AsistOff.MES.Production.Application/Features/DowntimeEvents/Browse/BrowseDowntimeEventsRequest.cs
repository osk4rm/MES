using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Browse;

public class BrowseDowntimeEventsRequest
    : ITenantRequest<PagedResponse<DowntimeEventResponse>>, IPagedRequest
{
    public Guid? MachineId { get; set; }
    public Guid? ReasonCodeId { get; set; }
    public DowntimeEventStatus? Status { get; set; }
    public DateTime? StartedFrom { get; set; }
    public DateTime? StartedTo { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["StartedAt", "EndedAt", "MachineId"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
