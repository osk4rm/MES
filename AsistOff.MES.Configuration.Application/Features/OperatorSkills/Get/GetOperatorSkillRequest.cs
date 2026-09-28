using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.OperatorSkills.Get;

public record GetOperatorSkillRequest(Guid Id) : ITenantRequest<OperatorSkillQualificationResponse>;
