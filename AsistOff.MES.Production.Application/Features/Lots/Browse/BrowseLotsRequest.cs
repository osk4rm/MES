using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.Lots.Browse;

public class BrowseLotsRequest
    : ITenantRequest<PagedResponse<LotResponse>>, IPagedRequest
{
    public string? Code { get; set; }
    public Guid? ProductId { get; set; }
    public LotStatus? Status { get; set; }
    public DateTime? ExpiryFrom { get; set; }
    public DateTime? ExpiryTo { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Code", "ProductId", "Status", "ExpiryDate"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
