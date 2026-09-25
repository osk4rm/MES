using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A lot-level genealogy edge: records that a quantity of a consumed
/// (component) lot went into a produced (finished good) lot under a
/// <see cref="ProductionOrder"/>, optionally linked to the
/// <see cref="ProductionConfirmation"/> that reported the work.
/// Recording is explicit via POST /api/lot-genealogy and automatic when a
/// confirmation carries lot references (one edge per consumed lot entry).
/// Same-lot self links are rejected. Corrections are
/// delete plus re-record; there is no update path.
/// </summary>
public class LotGenealogyEdge : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Component lot consumed (same tenant, FK with restrict delete).</summary>
    public Guid ConsumedLotId { get; set; }

    /// <summary>Finished good lot produced (same tenant, FK with restrict delete).</summary>
    public Guid ProducedLotId { get; set; }

    /// <summary>Order the edge is recorded against (same tenant, FK with cascade delete).</summary>
    public Guid ProductionOrderId { get; set; }

    /// <summary>Confirmation that reported the work (same tenant, optional, FK with set null on delete).</summary>
    public Guid? ProductionConfirmationId { get; set; }

    /// <summary>Work Center (Machine) the work was performed on. Stored as a loose Guid.</summary>
    public Guid MachineId { get; set; }

    /// <summary>Operator that reported the work. Optional, stored as a loose Guid.</summary>
    public Guid? ReportedByOperatorId { get; set; }

    /// <summary>Quantity of the consumed lot used. Must be greater than zero.</summary>
    public decimal ConsumedQuantity { get; set; }

    /// <summary>UTC timestamp of when the consumption happened.</summary>
    public DateTime OccurredAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Lot? ConsumedLot { get; set; }
    public virtual Lot? ProducedLot { get; set; }
    public virtual ProductionOrder? ProductionOrder { get; set; }
    public virtual ProductionConfirmation? ProductionConfirmation { get; set; }
}
