using AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Get;

internal sealed class GetOpcUaConnectionRequestHandler(IOpcUaConnectionsRepository repository)
    : IRequestHandler<GetOpcUaConnectionRequest, OpcUaConnectionResponse>
{
    public async Task<OpcUaConnectionResponse> Handle(GetOpcUaConnectionRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OpcUaConnection", request.Id);

        return BrowseOpcUaConnectionsRequestHandler.Map(entity);
    }
}
