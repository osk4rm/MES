using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Update;

internal sealed class UpdateScrapEventRequestHandler(
    IScrapEventsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateScrapEventRequest>
{
    public async Task Handle(UpdateScrapEventRequest request, CancellationToken cancellationToken)
    {
        var scrapEvent = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ScrapEvent", request.Id);

        if (request.ReasonCodeId == Guid.Empty)
            throw new ValidationException(nameof(request.ReasonCodeId), "Reason code is required.");

        if (request.Quantity <= 0)
            throw new ValidationException(nameof(request.Quantity), "Quantity must be greater than zero.");

        // Only Quantity, ReasonCodeId and Notes are editable.
        // ReportedAt and MachineId are immutable after create.
        scrapEvent.Quantity = request.Quantity;
        scrapEvent.ReasonCodeId = request.ReasonCodeId;
        scrapEvent.Notes = request.Notes;
        scrapEvent.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(scrapEvent, cancellationToken);
    }
}
