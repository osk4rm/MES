using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.Kanban;

public class PagedKanbanLoopsResponse(
    IReadOnlyCollection<KanbanLoopResponse> items, int totalCount, int? pageSize)
    : PagedResponse<KanbanLoopResponse>(items, totalCount, pageSize);

public class PagedKanbanCardsResponse(
    IReadOnlyCollection<KanbanCardResponse> items, int totalCount, int? pageSize)
    : PagedResponse<KanbanCardResponse>(items, totalCount, pageSize);
