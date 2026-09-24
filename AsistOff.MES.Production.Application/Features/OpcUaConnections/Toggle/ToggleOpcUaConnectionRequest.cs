using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Toggle;

public record ToggleOpcUaConnectionRequest(Guid Id) : ITenantRequest<OpcUaConnectionResponse>;
