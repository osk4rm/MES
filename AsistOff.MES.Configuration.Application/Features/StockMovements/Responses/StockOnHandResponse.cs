namespace AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;

/// <summary>
/// One signed stock balance: the sum of PW receipt quantities minus the sum
/// of RW issue quantities for a single product and warehouse pair. A null
/// <c>WarehouseId</c> is the unassigned bucket (ledger lines without a
/// warehouse hint) and is reported, never dropped.
/// <c>ReservedQuantity</c> is the unrelieved remainder of non-Closed soft
/// material reservations for the same pair (issue #291); <c>AvailableQuantity</c>
/// is on-hand minus reserved and may go negative (soft allocation only).
/// </summary>
public sealed record StockOnHandResponse(
    Guid ProductId,
    Guid? WarehouseId,
    decimal QuantityOnHand,
    decimal ReservedQuantity,
    decimal AvailableQuantity);
