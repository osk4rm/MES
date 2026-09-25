using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Delete;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record DeleteMachineTelemetryTagRequest(Guid Id) : ITenantRequest;
