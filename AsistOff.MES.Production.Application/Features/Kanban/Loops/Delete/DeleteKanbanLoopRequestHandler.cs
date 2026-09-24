using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Delete;

internal sealed class DeleteKanbanLoopRequestHandler(IKanbanLoopsRepository repository)
    : IRequestHandler<DeleteKanbanLoopRequest>
{
    public async Task Handle(DeleteKanbanLoopRequest request, CancellationToken cancellationToken)
    {
        _ = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KanbanLoop", request.Id);

        // Cards cascade-delete via the FK from production.KanbanCards.
        await repository.DeleteAsync(request.Id, cancellationToken);
    }
}
