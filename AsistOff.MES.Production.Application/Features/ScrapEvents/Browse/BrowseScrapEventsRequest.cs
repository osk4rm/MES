using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Browse;

public class BrowseScrapEventsRequest
    : ITenantRequest<PagedResponse<ScrapEventResponse>>, IPagedRequest
{
    public Guid? MachineId { get; set; }
    public Guid? ReasonCodeId { get; set; }
    public DateTime? ReportedFrom { get; set; }
    public DateTime? ReportedTo { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["ReportedAt", "Quantity"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
