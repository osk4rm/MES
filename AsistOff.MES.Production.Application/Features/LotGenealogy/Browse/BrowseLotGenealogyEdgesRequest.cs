using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Browse;

public class BrowseLotGenealogyEdgesRequest
    : ITenantRequest<PagedResponse<LotGenealogyEdgeResponse>>, IPagedRequest
{
    public Guid? ProducedLotId { get; set; }
    public Guid? ConsumedLotId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["OccurredAt", "CreatedAt"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
