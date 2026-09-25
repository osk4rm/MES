using AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.StockMovements.StockOnHand;

/// <summary>
/// Read-only stock-on-hand query over the persisted StockMovement ledger:
/// PW receipts add, RW issues subtract, grouped by product and warehouse.
/// Both filters are optional; omitting both returns all balances.
/// </summary>
public class GetStockOnHandRequest : ITenantRequest<IReadOnlyList<StockOnHandResponse>>
{
    public Guid? ProductId { get; set; }

    public Guid? WarehouseId { get; set; }
}
