using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Delete;

internal sealed class DeleteOpcUaConnectionRequestHandler(IOpcUaConnectionsRepository repository)
    : IRequestHandler<DeleteOpcUaConnectionRequest>
{
    public async Task Handle(DeleteOpcUaConnectionRequest request, CancellationToken cancellationToken)
    {
        _ = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OpcUaConnection", request.Id);

        // Delete cascades nothing: readings stay attached to tags, which are
        // independent of connections in this slice.
        await repository.DeleteAsync(request.Id, cancellationToken);
    }
}
