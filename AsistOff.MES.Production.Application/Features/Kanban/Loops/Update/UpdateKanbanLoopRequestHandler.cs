using AsistOff.MES.Production.Application.Features.Kanban;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Kanban.Loops.Update;

internal sealed class UpdateKanbanLoopRequestHandler(
    IKanbanLoopsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateKanbanLoopRequest>
{
    public async Task Handle(UpdateKanbanLoopRequest request, CancellationToken cancellationToken)
    {
        var loop = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("KanbanLoop", request.Id);

        KanbanMappings.ValidateLoopQuantities(request.CardQuantity, request.CardsInCirculation);

        if (request.Notes is { Length: > 1000 })
            throw new ValidationException(nameof(request.Notes), "Notes must not exceed 1000 characters.");

        loop.CardQuantity = request.CardQuantity;
        loop.CardsInCirculation = request.CardsInCirculation;
        loop.IsActive = request.IsActive;
        loop.Notes = request.Notes;
        loop.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(loop, cancellationToken);
    }
}
