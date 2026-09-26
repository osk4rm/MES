using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.MaterialReservations;

/// <summary>
/// Paged read of soft material reservations (issue #291). All filters are
/// optional; omitting them lists every reservation of the current tenant.
/// </summary>
public class BrowseMaterialReservationsRequest
    : ITenantRequest<PagedResponse<MaterialReservationResponse>>, IPagedRequest
{
    public Guid? ProductionOrderId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public ReservationStatus? Status { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } =
        ["CreatedAt", "ProductId", "WarehouseId", "ProductionOrderId", "Status"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
