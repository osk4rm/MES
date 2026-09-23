using AsistOff.MES.Production.Application.Features.DowntimeEvents.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Get;

internal sealed class GetDowntimeEventRequestHandler(IDowntimeEventsRepository repository)
    : IRequestHandler<GetDowntimeEventRequest, DowntimeEventResponse>
{
    public async Task<DowntimeEventResponse> Handle(GetDowntimeEventRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("DowntimeEvent", request.Id);

        return BrowseDowntimeEventsRequestHandler.Map(entity);
    }
}
