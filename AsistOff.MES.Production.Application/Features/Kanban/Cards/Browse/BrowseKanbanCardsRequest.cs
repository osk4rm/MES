using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Browse;

public class BrowseKanbanCardsRequest
    : ITenantRequest<PagedResponse<KanbanCardResponse>>, IPagedRequest
{
    public Guid? LoopId { get; set; }
    public KanbanCardStatus? Status { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["CardNumber", "Status"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
