using AsistOff.MES.Configuration.Application.Features.OperatorSkills.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.OperatorSkills.Browse;

public class BrowseOperatorSkillsRequest
    : ITenantRequest<PagedResponse<OperatorSkillQualificationResponse>>, IPagedRequest
{
    public Guid? OperatorId { get; set; }
    public Guid? SkillId { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } =
        ["OperatorId", "SkillId"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
