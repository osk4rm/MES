using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// Soft material allocation created when a Production Order is released
/// (issue #291). One row per distinct (ProductionOrderId, ProductId,
/// WarehouseId) aggregates the released recipe BOM requirements scaled by
/// the order quantity. Operator confirmations relieve the reservation via
/// their RW movement lines; closing the order closes any remainder.
/// A null <c>WarehouseId</c> is the unassigned bucket (BOM items without a
/// preferred warehouse), mirroring <see cref="StockMovement.WarehouseId"/>.
/// Reservations never block release: a shortage surfaces as negative
/// availability on the stock read, not as a rejection.
/// </summary>
public class MaterialReservation : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Order whose release created this reservation.</summary>
    public Guid ProductionOrderId { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>
    /// Preferred warehouse from the BOM item, or null when the BOM carries
    /// no warehouse hint (the Production Order itself has no warehouse).
    /// </summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Total required quantity reserved at release (always &gt; 0).</summary>
    public decimal QuantityReserved { get; set; }

    /// <summary>Quantity already relieved by RW confirmation postings.</summary>
    public decimal QuantityRelieved { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Active;

    /// <summary>Unrelieved remainder driving the availability computation.</summary>
    public decimal RemainingQuantity => QuantityReserved - QuantityRelieved;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
}
