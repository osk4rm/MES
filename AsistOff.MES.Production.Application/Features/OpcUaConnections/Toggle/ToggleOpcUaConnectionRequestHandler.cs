using AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Toggle;

internal sealed class ToggleOpcUaConnectionRequestHandler(
    IOpcUaConnectionsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ToggleOpcUaConnectionRequest, OpcUaConnectionResponse>
{
    public async Task<OpcUaConnectionResponse> Handle(ToggleOpcUaConnectionRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OpcUaConnection", request.Id);

        entity.IsEnabled = !entity.IsEnabled;
        entity.UpdatedAt = dateTimeProvider.UtcNow;

        await repository.UpdateAsync(entity, cancellationToken);
        return BrowseOpcUaConnectionsRequestHandler.Map(entity);
    }
}
