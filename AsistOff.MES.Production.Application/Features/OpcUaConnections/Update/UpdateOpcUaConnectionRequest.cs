using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Update;

/// <summary>
/// Updates connection dictionary fields. <c>MachineId</c> is immutable after
/// create so the (tenant, machine, endpoint) uniqueness can never break.
/// </summary>
public record UpdateOpcUaConnectionRequest(
    Guid Id,
    string EndpointUrl,
    OpcUaSecurityPolicy SecurityPolicy,
    int PollIntervalSeconds) : ITenantRequest;
