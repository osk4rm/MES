using AsistOff.MES.Production.Application.Features.DowntimeEvents.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Close;

internal sealed class CloseDowntimeEventRequestHandler(
    IDowntimeEventsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CloseDowntimeEventRequest, DowntimeEventResponse>
{
    public async Task<DowntimeEventResponse> Handle(CloseDowntimeEventRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("DowntimeEvent", request.Id);

        if (entity.EndedAt.HasValue)
            throw new ConflictException("The downtime event is already closed.");

        var endedAt = request.EndedAt ?? dateTimeProvider.UtcNow;

        if (endedAt < entity.StartedAt)
            throw new ValidationException(nameof(request.EndedAt), "End time cannot be earlier than start time.");

        entity.EndedAt = endedAt;
        entity.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(entity, cancellationToken);
        return BrowseDowntimeEventsRequestHandler.Map(entity);
    }
}
