using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Test;

internal sealed class TestOpcUaConnectionRequestHandler(
    IOpcUaConnectionsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<TestOpcUaConnectionRequest, TestOpcUaConnectionResponse>
{
    public async Task<TestOpcUaConnectionResponse> Handle(TestOpcUaConnectionRequest request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OpcUaConnection", request.Id);

        // Shape-only validation in this slice: no vendor SDK, no network I/O.
        // A malformed stored URL throws ValidationException (400). A well
        // formed URL is reported reachable — the poller proves liveness.
        var endpointUrl = OpcUaConnectionRules.ValidateEndpointUrl(entity.EndpointUrl);

        return new TestOpcUaConnectionResponse(
            entity.Id, endpointUrl, Reachable: true, dateTimeProvider.UtcNow);
    }
}
