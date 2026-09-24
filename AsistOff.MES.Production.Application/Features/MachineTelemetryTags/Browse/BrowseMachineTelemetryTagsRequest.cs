using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Browse;

public class BrowseMachineTelemetryTagsRequest
    : ITenantRequest<PagedResponse<MachineTelemetryTagResponse>>, IPagedRequest
{
    public Guid? MachineId { get; set; }
    public bool? IsEnabled { get; set; }
    public string? Search { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["NodeId", "DisplayName", "CreatedAt"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
