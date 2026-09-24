using AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.MaintenanceWorkOrders.Browse;

public class BrowseMaintenanceWorkOrdersRequest
    : ITenantRequest<PagedResponse<MaintenanceWorkOrderResponse>>, IPagedRequest
{
    public Guid? MachineId { get; set; }
    public MaintenanceWorkOrderStatus? Status { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } =
        ["Code", "Title", "Priority", "Status", "ReportedAt"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
