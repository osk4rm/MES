using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Update;

internal sealed class UpdateMachineTelemetryTagRequestHandler(
    IMachineTelemetryTagsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateMachineTelemetryTagRequest>
{
    public async Task Handle(UpdateMachineTelemetryTagRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MachineTelemetryTag", request.Id);

        MachineTelemetryTagRules.Validate(
            entity.MachineId, entity.NodeId, request.DisplayName, request.DataType, request.PollIntervalSeconds);

        entity.DisplayName = request.DisplayName.Trim();
        entity.DataType = request.DataType;
        entity.PollIntervalSeconds = request.PollIntervalSeconds;
        entity.IsEnabled = request.IsEnabled;
        entity.Description = request.Description;
        entity.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(entity, cancellationToken);
    }
}
