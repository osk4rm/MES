namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Test;

public record TestOpcUaConnectionResponse(
    Guid Id,
    string EndpointUrl,
    bool Reachable,
    DateTime CheckedAt);
