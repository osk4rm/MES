using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse;

public class BrowseOpcUaConnectionsRequest
    : ITenantRequest<PagedResponse<OpcUaConnectionResponse>>, IPagedRequest
{
    public Guid? MachineId { get; set; }
    public bool? IsEnabled { get; set; }
    public string? Search { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["EndpointUrl", "CreatedAt"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
