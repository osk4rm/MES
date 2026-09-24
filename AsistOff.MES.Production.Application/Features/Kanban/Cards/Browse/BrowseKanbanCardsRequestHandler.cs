using AsistOff.MES.Production.Application.Features.Kanban;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Browse;

internal sealed class BrowseKanbanCardsRequestHandler(IKanbanCardsRepository repository)
    : IRequestHandler<BrowseKanbanCardsRequest, PagedResponse<KanbanCardResponse>>
{
    public async Task<PagedResponse<KanbanCardResponse>> Handle(BrowseKanbanCardsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<KanbanCard>(true);

        if (request.LoopId.HasValue)
            predicate = predicate.And(x => x.LoopId == request.LoopId.Value);

        // Unknown or cross-tenant loop ids intentionally return zero rows, not 404:
        // the global tenant filter hides foreign loops and there is no loop lookup here.
        var statuses = request.Statuses;
        if (statuses is { Count: > 0 })
            predicate = predicate.And(x => statuses.Contains(x.Status));
        else if (request.Status.HasValue)
            predicate = predicate.And(x => x.Status == request.Status.Value);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<KanbanCard>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedKanbanCardsResponse(
            items.Select(KanbanMappings.Map).ToList(), totalCount, request.PageSize);
    }
}
