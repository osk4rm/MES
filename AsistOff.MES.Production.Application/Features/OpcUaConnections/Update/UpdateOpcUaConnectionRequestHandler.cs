using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Update;

internal sealed class UpdateOpcUaConnectionRequestHandler(
    IOpcUaConnectionsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateOpcUaConnectionRequest>
{
    public async Task Handle(UpdateOpcUaConnectionRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OpcUaConnection", request.Id);

        OpcUaConnectionRules.Validate(entity.MachineId, request.EndpointUrl, request.SecurityPolicy, request.PollIntervalSeconds);

        var endpointUrl = request.EndpointUrl.Trim();
        if (!string.Equals(endpointUrl, entity.EndpointUrl, StringComparison.Ordinal)
            && await repository.GetByEndpointAsync(entity.MachineId, endpointUrl, cancellationToken) is not null)
            throw new ConflictException($"An OPC UA connection for endpoint '{endpointUrl}' already exists on this Work Center.");

        entity.EndpointUrl = endpointUrl;
        entity.SecurityPolicy = request.SecurityPolicy;
        entity.PollIntervalSeconds = request.PollIntervalSeconds;
        entity.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(entity, cancellationToken);
    }
}
