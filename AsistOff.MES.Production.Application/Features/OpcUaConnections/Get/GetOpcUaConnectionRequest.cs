using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Get;

public record GetOpcUaConnectionRequest(Guid Id) : ITenantRequest<OpcUaConnectionResponse>;
