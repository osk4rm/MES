using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// Persisted RW/PW stock ledger line posted when a
/// <c>ProductionConfirmation</c> is created. One PW line carries the order
/// product receipt; one RW line per released-recipe BOM item carries the
/// material issue. Corrections follow the delete plus re-create model: the
/// FK to the confirmation cascades, so deleting a confirmation removes its
/// lines and re-creating posts a fresh set. No backfill for confirmations
/// created before this ledger existed.
/// </summary>
public class StockMovement : IEntity, ISaasy, IAuditable
{
    /// <summary>Finished goods receipt (Przyjecie Wewnetrzne). Mirrors MovementCalculator.ReceiptType.</summary>
    public const string ReceiptType = "PW";

    /// <summary>Material issue (Rozchod Wewnetrzny). Mirrors MovementCalculator.IssueType.</summary>
    public const string IssueType = "RW";

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Either <c>"PW"</c> or <c>"RW"</c>.</summary>
    public required string MovementType { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>Unsigned ledger quantity, same value the movement preview returns.</summary>
    public decimal Quantity { get; set; }

    public Guid? MeasureUnitId { get; set; }

    /// <summary>Planning hint taken from the BOM item; null for PW lines.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Confirmation that posted this line (FK with cascade delete).</summary>
    public Guid ProductionConfirmationId { get; set; }

    /// <summary>Denormalized order id for filtering.</summary>
    public Guid ProductionOrderId { get; set; }

    public DateTime ReportedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
}
