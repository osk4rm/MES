using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Toggle;

internal sealed class ToggleMachineTelemetryTagRequestHandler(
    IMachineTelemetryTagsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ToggleMachineTelemetryTagRequest, MachineTelemetryTagResponse>
{
    public async Task<MachineTelemetryTagResponse> Handle(ToggleMachineTelemetryTagRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MachineTelemetryTag", request.Id);

        entity.IsEnabled = !entity.IsEnabled;
        entity.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(entity, cancellationToken);
        return BrowseMachineTelemetryTagsRequestHandler.Map(entity);
    }
}
