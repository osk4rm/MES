using AsistOff.MES.Configuration.Application.Features.Skills.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Create;

public record CreateSkillRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive) : ITenantRequest<SkillResponse>;
