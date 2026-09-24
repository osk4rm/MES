using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Get;

internal sealed class GetMachineTelemetryTagRequestHandler(IMachineTelemetryTagsRepository repository)
    : IRequestHandler<GetMachineTelemetryTagRequest, MachineTelemetryTagResponse>
{
    public async Task<MachineTelemetryTagResponse> Handle(GetMachineTelemetryTagRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MachineTelemetryTag", request.Id);

        return BrowseMachineTelemetryTagsRequestHandler.Map(entity);
    }
}
