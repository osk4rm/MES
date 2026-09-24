using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// One fixed-quantity container circulating in a <see cref="KanbanLoop"/>.
/// Pull-signal status transitions (consume/order/replenish) are a follow-up;
/// this slice only records the registry.
/// </summary>
public class KanbanCard : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Owning loop (same tenant, FK with cascade delete).</summary>
    public Guid LoopId { get; set; }

    /// <summary>Unique per loop, e.g. LOOPCODE-01.</summary>
    public required string CardNumber { get; set; }

    public KanbanCardStatus Status { get; set; } = KanbanCardStatus.Full;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual KanbanLoop? Loop { get; set; }
}
