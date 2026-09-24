using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Browse;

public class BrowseProductionConfirmationsRequest
    : ITenantRequest<PagedResponse<ProductionConfirmationResponse>>, IPagedRequest
{
    public Guid? ProductionOrderId { get; set; }
    public Guid? MachineId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["ReportedAt", "CreatedAt"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
