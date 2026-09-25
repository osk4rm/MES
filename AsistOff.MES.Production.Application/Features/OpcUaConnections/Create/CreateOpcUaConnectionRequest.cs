using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Create;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record CreateOpcUaConnectionRequest(
    Guid MachineId,
    string EndpointUrl,
    OpcUaSecurityPolicy SecurityPolicy,
    int PollIntervalSeconds) : ITenantRequest<OpcUaConnectionResponse>;
