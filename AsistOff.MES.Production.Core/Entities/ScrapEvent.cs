using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A runtime record of scrap reported against a Work Center (Machine) with a
/// reason code, a quantity and a timestamp. The optional
/// <see cref="ProductionOrderId"/> link is reserved for a future increment and
/// must stay null for now.
/// </summary>
public class ScrapEvent : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Work Center (Machine) the scrap was reported against. Stored as a loose Guid.</summary>
    public Guid MachineId { get; set; }

    /// <summary>Reason code (Scrap category) explaining the scrap. Stored as a loose Guid.</summary>
    public Guid ReasonCodeId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>UTC timestamp of when the scrap occurred (immutable after create).</summary>
    public DateTime ReportedAt { get; set; }

    public string? Notes { get; set; }

    /// <summary>Operator that reported the scrap. Optional, stored as a loose Guid.</summary>
    public Guid? ReportedByOperatorId { get; set; }

    /// <summary>Reserved link to a Production Order. Never set in this increment.</summary>
    public Guid? ProductionOrderId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
