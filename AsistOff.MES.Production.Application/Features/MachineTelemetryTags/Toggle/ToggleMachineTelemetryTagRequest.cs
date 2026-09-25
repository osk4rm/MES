using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Toggle;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record ToggleMachineTelemetryTagRequest(Guid Id) : ITenantRequest<MachineTelemetryTagResponse>;
