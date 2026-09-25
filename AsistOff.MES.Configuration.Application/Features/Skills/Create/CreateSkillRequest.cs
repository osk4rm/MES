using AsistOff.MES.Configuration.Application.Features.Skills.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateSkillRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive) : ITenantRequest<SkillResponse>;
