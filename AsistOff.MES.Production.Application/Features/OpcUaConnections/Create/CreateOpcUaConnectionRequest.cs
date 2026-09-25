using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Create;

public record CreateOpcUaConnectionRequest(
    Guid MachineId,
    string EndpointUrl,
    OpcUaSecurityPolicy SecurityPolicy,
    int PollIntervalSeconds) : ITenantRequest<OpcUaConnectionResponse>;
