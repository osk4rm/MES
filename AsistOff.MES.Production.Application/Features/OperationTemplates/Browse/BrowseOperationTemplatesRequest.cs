using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.OperationTemplates.Browse;

public class BrowseOperationTemplatesRequest
    : ITenantRequest<PagedResponse<OperationTemplateResponse>>, IPagedRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public bool? IsActive { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Name", "Code", "IsActive"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
