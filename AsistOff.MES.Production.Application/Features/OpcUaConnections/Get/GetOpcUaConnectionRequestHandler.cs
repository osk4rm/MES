using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Application.Features.OpcUaConnections.Browse;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Get;

internal sealed class GetOpcUaConnectionRequestHandler(
    IOpcUaConnectionsRepository repository,
    ICurrentPermissionsAccessor permissionsAccessor)
    : IRequestHandler<GetOpcUaConnectionRequest, OpcUaConnectionResponse>
{
    public async Task<OpcUaConnectionResponse> Handle(GetOpcUaConnectionRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OpcUaConnection", request.Id);

        var response = BrowseOpcUaConnectionsRequestHandler.Map(entity);

        // Issue #372: read-only callers get no raw provider error text.
        if (!SensitiveProjectionPolicy.CanSeeSensitiveDetails(permissionsAccessor.Permissions))
            response = response with { LastError = null };

        return response;
    }
}
