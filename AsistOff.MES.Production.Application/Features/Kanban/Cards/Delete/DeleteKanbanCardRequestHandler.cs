using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Cards.Delete;

internal sealed class DeleteKanbanCardRequestHandler(IKanbanCardsRepository repository)
    : IRequestHandler<DeleteKanbanCardRequest>
{
    public async Task Handle(DeleteKanbanCardRequest request, CancellationToken cancellationToken)
    {
        _ = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KanbanCard", request.Id);

        await repository.DeleteAsync(request.Id, cancellationToken);
    }
}
