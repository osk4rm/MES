using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Browse;

public class BrowseProductionOrdersRequest
    : ITenantRequest<PagedResponse<ProductionOrderResponse>>, IPagedRequest
{
    public string? Code { get; set; }
    public ProductionOrderStatus? Status { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? RecipeId { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Code", "Status", "Priority", "DueDate", "CreatedAt"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
