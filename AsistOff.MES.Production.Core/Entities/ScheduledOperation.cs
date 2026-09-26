using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A persisted manual schedule override for one operation of a Production Order
/// (issue #304, Gantt slice 1/3): the planner-adjusted placement of an
/// <see cref="OperationNode"/> on a Work Center (stored as
/// <see cref="MachineId"/>) with a fixed <see cref="PlannedStart"/> /
/// <see cref="PlannedEnd"/> window. The computed Gantt read-model overlays
/// these rows on top of the recomputed schedule so manual adjustments survive
/// recomputation; drag/resize writes arrive in slice 2/3.
/// </summary>
public class ScheduledOperation : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Production Order the overridden operation belongs to.</summary>
    public Guid ProductionOrderId { get; set; }

    /// <summary>Operation node the override applies to.</summary>
    public Guid OperationNodeId { get; set; }

    /// <summary>Work Center (Machine) the operation is pinned to. Stored as a loose Guid.</summary>
    public Guid MachineId { get; set; }

    /// <summary>Planner-fixed UTC start of the operation segment.</summary>
    public DateTime PlannedStart { get; set; }

    /// <summary>Planner-fixed UTC end of the operation segment.</summary>
    public DateTime PlannedEnd { get; set; }

    /// <summary>Optional planner note explaining the manual adjustment.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
}
