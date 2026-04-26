using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.Skills.Update;

public record UpdateSkillRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive) : ITenantRequest;
