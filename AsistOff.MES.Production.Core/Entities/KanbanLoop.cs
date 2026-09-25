using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A pull-based replenishment loop: product X consumed at a Work Center is
/// replenished from a warehouse in fixed lot sizes. Each loop owns a registry
/// of circulating <see cref="KanbanCard"/> containers.
/// </summary>
public class KanbanLoop : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }

    /// <summary>Replenished product. Stored as a loose Guid to Configuration.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Consuming Work Center (Machine). Stored as a loose Guid to Configuration.</summary>
    public Guid ConsumingMachineId { get; set; }

    /// <summary>Supplying warehouse. Stored as a loose Guid to Configuration.</summary>
    public Guid SupplyingWarehouseId { get; set; }

    /// <summary>Fixed container quantity, in the product base Unit of Measure.</summary>
    public decimal CardQuantity { get; set; }

    /// <summary>Number of cards allowed in circulation (1..100).</summary>
    public int CardsInCirculation { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }

    public virtual ICollection<KanbanCard> Cards { get; set; } = new List<KanbanCard>();
}
