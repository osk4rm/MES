using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Update;

/// <summary>
/// Updates connection dictionary fields. <c>MachineId</c> is immutable after
/// create so the (tenant, machine, endpoint) uniqueness can never break.
/// </summary>
[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateOpcUaConnectionRequest(
    Guid Id,
    string EndpointUrl,
    OpcUaSecurityPolicy SecurityPolicy,
    int PollIntervalSeconds) : ITenantRequest;
