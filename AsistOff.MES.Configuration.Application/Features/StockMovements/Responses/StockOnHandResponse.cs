namespace AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;

/// <summary>
/// One signed stock balance: the sum of PW receipt quantities minus the sum
/// of RW issue quantities for a single product and warehouse pair. A null
/// <c>WarehouseId</c> is the unassigned bucket (ledger lines without a
/// warehouse hint) and is reported, never dropped.
/// </summary>
public sealed record StockOnHandResponse(
    Guid ProductId,
    Guid? WarehouseId,
    decimal QuantityOnHand);
