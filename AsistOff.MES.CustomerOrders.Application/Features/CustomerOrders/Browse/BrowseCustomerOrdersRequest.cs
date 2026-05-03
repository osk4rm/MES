using AsistOff.MES.CustomerOrders.Application.Features.Common;
using AsistOff.MES.CustomerOrders.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.CustomerOrders.Application.Features.CustomerOrders.Browse;

public class BrowseCustomerOrdersRequest : ITenantRequest<PagedResponse<CustomerOrderResponse>>, IPagedRequest
{
    public string? OrderNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? ProductId { get; set; }
    public CustomerOrderStatus? Status { get; set; }
    public string? ExternalSystem { get; set; }
    public DateTime? OrderDateFrom { get; set; }
    public DateTime? OrderDateTo { get; set; }
    public DateTime? DeliveryDateFrom { get; set; }
    public DateTime? DeliveryDateTo { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["OrderNumber", "Status", "OrderDate", "RequestedDeliveryDate", "CustomerNameSnapshot"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
