using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Delete;

internal sealed class DeleteScrapEventRequestHandler(IScrapEventsRepository repository)
    : IRequestHandler<DeleteScrapEventRequest>
{
    public async Task Handle(DeleteScrapEventRequest request, CancellationToken cancellationToken)
    {
        var scrapEvent = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ScrapEvent", request.Id);

        await repository.DeleteAsync(scrapEvent.Id, cancellationToken);
    }
}
