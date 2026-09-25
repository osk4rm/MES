using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Update;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record UpdateSkillRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive) : ITenantRequest;
