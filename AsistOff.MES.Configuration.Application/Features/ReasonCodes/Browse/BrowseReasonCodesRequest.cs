using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Browse;

public class BrowseReasonCodesRequest
    : ITenantRequest<PagedResponse<ReasonCodeResponse>>, IPagedRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public ReasonCodeCategory? Category { get; set; }
    public bool? IsActive { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } =
        ["Code", "Name", "Category", "IsActive", "SortIndex"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
