using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.OperatorSkills.Browse;

internal sealed class BrowseOperatorSkillsRequestHandler(
    IOperatorSkillQualificationsRepository repository)
    : IRequestHandler<BrowseOperatorSkillsRequest, PagedResponse<OperatorSkillQualificationResponse>>
{
    public async Task<PagedResponse<OperatorSkillQualificationResponse>> Handle(
        BrowseOperatorSkillsRequest request, CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<OperatorSkillQualification>(true);

        if (request.OperatorId.HasValue)
            predicate = predicate.And(x => x.OperatorId == request.OperatorId.Value);
        if (request.SkillId.HasValue)
            predicate = predicate.And(x => x.SkillId == request.SkillId.Value);

        var totalCount = await repository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<OperatorSkillQualification>(predicate, request);
        var items = await repository.BrowseAsync(paginator, cancellationToken);

        var result = items.Select(Map).ToList();
        return new PagedOperatorSkillsResponse(result, totalCount, request.PageSize);
    }

    internal static OperatorSkillQualificationResponse Map(OperatorSkillQualification q) => new(
        q.Id,
        q.OperatorId,
        q.Operator?.Identifier,
        q.Operator is null ? null : $"{q.Operator.FirstName} {q.Operator.LastName}".Trim(),
        q.SkillId,
        q.Skill?.Code,
        q.Skill?.Name);
}
