using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Toggle;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record ToggleOpcUaConnectionRequest(Guid Id) : ITenantRequest<OpcUaConnectionResponse>;
