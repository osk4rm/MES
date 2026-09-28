namespace AsistOff.MES.Configuration.Application.Features.OperatorSkills.Responses;

public record OperatorSkillQualificationResponse(
    Guid Id,
    Guid OperatorId,
    string? OperatorIdentifier,
    string? OperatorName,
    Guid SkillId,
    string? SkillCode,
    string? SkillName);

public class PagedOperatorSkillsResponse(
    IReadOnlyCollection<OperatorSkillQualificationResponse> items,
    int totalCount,
    int? pageSize)
    : AsistOff.MES.Shared.Abstractions.Contracts.Paging.PagedResponse<OperatorSkillQualificationResponse>(items, totalCount, pageSize);
