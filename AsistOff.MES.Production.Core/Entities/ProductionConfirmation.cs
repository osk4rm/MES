using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// An append-only operator report of work performed against a
/// <see cref="ProductionOrder"/>: good and scrap quantities produced on a
/// Work Center at a given UTC time. Corrections are delete plus re-create;
/// there is no update path for this log.
/// </summary>
public class ProductionConfirmation : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Order the work is reported against (same tenant, FK with cascade delete).</summary>
    public Guid ProductionOrderId { get; set; }

    /// <summary>Work Center (Machine) the work was performed on. Stored as a loose Guid.</summary>
    public Guid MachineId { get; set; }

    /// <summary>Operator that reported the work. Optional, stored as a loose Guid.</summary>
    public Guid? ReportedByOperatorId { get; set; }

    /// <summary>UTC timestamp of when the work was performed (immutable after create).</summary>
    public DateTime ReportedAt { get; set; }

    public decimal GoodQuantity { get; set; }

    public decimal ScrapQuantity { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ProductionOrder? ProductionOrder { get; set; }
}
