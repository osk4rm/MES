using AsistOff.MES.Production.Application.Features.ScrapEvents.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Get;

internal sealed class GetScrapEventRequestHandler(IScrapEventsRepository repository)
    : IRequestHandler<GetScrapEventRequest, ScrapEventResponse>
{
    public async Task<ScrapEventResponse> Handle(GetScrapEventRequest request, CancellationToken cancellationToken)
    {
        var scrapEvent = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ScrapEvent", request.Id);

        return BrowseScrapEventsRequestHandler.Map(scrapEvent);
    }
}
