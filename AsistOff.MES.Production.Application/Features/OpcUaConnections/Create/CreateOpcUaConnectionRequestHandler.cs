using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Create;

internal sealed class CreateOpcUaConnectionRequestHandler(
    IOpcUaConnectionsRepository repository,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext)
    : IRequestHandler<CreateOpcUaConnectionRequest, OpcUaConnectionResponse>
{
    public async Task<OpcUaConnectionResponse> Handle(CreateOpcUaConnectionRequest request, CancellationToken cancellationToken)
    {
        OpcUaConnectionRules.Validate(request.MachineId, request.EndpointUrl, request.SecurityPolicy, request.PollIntervalSeconds);

        var endpointUrl = request.EndpointUrl.Trim();
        if (await repository.GetByEndpointAsync(request.MachineId, endpointUrl, cancellationToken) is not null)
            throw new ConflictException($"An OPC UA connection for endpoint '{endpointUrl}' already exists on this Work Center.");

        var entity = new OpcUaConnection
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantContext.TenantId,
            MachineId = request.MachineId,
            EndpointUrl = endpointUrl,
            SecurityPolicy = request.SecurityPolicy,
            PollIntervalSeconds = request.PollIntervalSeconds,
            IsEnabled = true,
            LastSeenAtUtc = null,
            LastError = null,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await repository.AddAsync(entity, cancellationToken);
        return BrowseOpcUaConnectionsRequestHandler.Map(entity);
    }
}
