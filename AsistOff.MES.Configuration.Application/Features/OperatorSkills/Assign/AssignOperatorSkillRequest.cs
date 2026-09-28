using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.OperatorSkills.Assign;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record AssignOperatorSkillRequest(
    Guid OperatorId,
    Guid SkillId) : ITenantRequest<OperatorSkillQualificationResponse>;
