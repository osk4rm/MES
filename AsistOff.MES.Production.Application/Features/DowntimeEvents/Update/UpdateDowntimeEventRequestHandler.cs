using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Update;

internal sealed class UpdateDowntimeEventRequestHandler(
    IDowntimeEventsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateDowntimeEventRequest>
{
    public async Task Handle(UpdateDowntimeEventRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("DowntimeEvent", request.Id);

        if (entity.EndedAt.HasValue)
            throw new ConflictException("Only open downtime events can be updated.");

        if (request.ReasonCodeId == Guid.Empty)
            throw new ValidationException(nameof(request.ReasonCodeId), "Reason code is required.");

        entity.ReasonCodeId = request.ReasonCodeId;
        entity.Notes = request.Notes;
        entity.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(entity, cancellationToken);
    }
}
