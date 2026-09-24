using AsistOff.MES.Production.Application.Features.Kanban;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Get;

internal sealed class GetKanbanLoopRequestHandler(IKanbanLoopsRepository repository)
    : IRequestHandler<GetKanbanLoopRequest, KanbanLoopResponse>
{
    public async Task<KanbanLoopResponse> Handle(GetKanbanLoopRequest request, CancellationToken cancellationToken)
    {
        var loop = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KanbanLoop", request.Id);

        return KanbanMappings.Map(loop);
    }
}
