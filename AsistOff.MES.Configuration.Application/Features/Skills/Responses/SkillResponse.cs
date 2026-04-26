namespace AsistOff.MES.Configuration.Application.Features.Skills.Responses;

public record SkillResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public class PagedSkillsResponse(IReadOnlyCollection<SkillResponse> items, int totalCount, int? pageSize)
    : AsistOff.MES.Shared.Abstractions.Contracts.Paging.PagedResponse<SkillResponse>(items, totalCount, pageSize);
