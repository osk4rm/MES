using AsistOff.MES.Production.Application.Features.Kanban;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Browse;

internal sealed class BrowseKanbanLoopsRequestHandler(IKanbanLoopsRepository repository)
    : IRequestHandler<BrowseKanbanLoopsRequest, PagedResponse<KanbanLoopResponse>>
{
    public async Task<PagedResponse<KanbanLoopResponse>> Handle(BrowseKanbanLoopsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<KanbanLoop>(true);

        if (!string.IsNullOrWhiteSpace(request.Code))
            predicate = predicate.And(x => x.Code.Contains(request.Code));
        if (request.ProductId.HasValue)
            predicate = predicate.And(x => x.ProductId == request.ProductId.Value);
        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.ConsumingMachineId == request.MachineId.Value);
        if (request.IsActive.HasValue)
            predicate = predicate.And(x => x.IsActive == request.IsActive.Value);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<KanbanLoop>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        return new PagedKanbanLoopsResponse(
            items.Select(KanbanMappings.Map).ToList(), totalCount, request.PageSize);
    }
}
