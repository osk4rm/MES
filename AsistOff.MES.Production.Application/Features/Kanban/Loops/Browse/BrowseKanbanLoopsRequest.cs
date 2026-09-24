using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Browse;

public class BrowseKanbanLoopsRequest
    : ITenantRequest<PagedResponse<KanbanLoopResponse>>, IPagedRequest
{
    public string? Code { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? MachineId { get; set; }
    public bool? IsActive { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Code", "ProductId", "IsActive"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
