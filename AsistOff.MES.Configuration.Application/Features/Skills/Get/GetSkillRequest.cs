using AsistOff.MES.Configuration.Application.Features.Skills.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Get;

public record GetSkillRequest(Guid Id) : ITenantRequest<SkillResponse>;
