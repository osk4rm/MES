using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Create;

internal sealed class CreateMachineTelemetryTagRequestHandler(
    IMachineTelemetryTagsRepository repository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateMachineTelemetryTagRequest, MachineTelemetryTagResponse>
{
    public async Task<MachineTelemetryTagResponse> Handle(CreateMachineTelemetryTagRequest request, CancellationToken cancellationToken)
    {
        MachineTelemetryTagRules.Validate(request.MachineId, request.NodeId, request.DisplayName, request.DataType, request.PollIntervalSeconds);

        var nodeId = request.NodeId.Trim();
        if (await repository.GetByNodeAsync(request.MachineId, nodeId, cancellationToken) is not null)
            throw new ConflictException($"A telemetry tag for node '{nodeId}' already exists on this Work Center.");

        var entity = new MachineTelemetryTag
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            MachineId = request.MachineId,
            NodeId = nodeId,
            DisplayName = request.DisplayName.Trim(),
            DataType = request.DataType,
            PollIntervalSeconds = request.PollIntervalSeconds,
            IsEnabled = true,
            Description = request.Description,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await repository.AddAsync(entity, cancellationToken);
        return BrowseMachineTelemetryTagsRequestHandler.Map(entity);
    }
}
