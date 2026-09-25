using AsistOff.MES.Production.Application.Features.Kanban;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Get;

internal sealed class GetKanbanCardRequestHandler(IKanbanCardsRepository repository)
    : IRequestHandler<GetKanbanCardRequest, KanbanCardResponse>
{
    public async Task<KanbanCardResponse> Handle(GetKanbanCardRequest request, CancellationToken cancellationToken)
    {
        var card = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KanbanCard", request.Id);

        return KanbanMappings.Map(card);
    }
}
