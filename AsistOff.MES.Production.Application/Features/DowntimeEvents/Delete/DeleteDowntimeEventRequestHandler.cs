using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Delete;

internal sealed class DeleteDowntimeEventRequestHandler(IDowntimeEventsRepository repository)
    : IRequestHandler<DeleteDowntimeEventRequest>
{
    public async Task Handle(DeleteDowntimeEventRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("DowntimeEvent", request.Id);

        if (entity.EndedAt.HasValue)
            throw new ConflictException("Only open downtime events can be deleted.");

        await repository.DeleteAsync(entity.Id, cancellationToken);
    }
}
