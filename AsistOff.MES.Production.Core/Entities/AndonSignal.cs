using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A visual Andon signal raised against a Work Center (stored as <see cref="MachineId"/>)
/// to flag an abnormal condition (downtime, quality, material shortage, other).
/// Lifecycle: Active → Acknowledged → Resolved. Resolved signals are immutable.
/// The <see cref="ProductionOrderId"/> column is reserved for a future increment
/// and must stay null for now.
/// </summary>
public class AndonSignal : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid MachineId { get; set; }
    public AndonSignalCategory Category { get; set; }
    public Guid? ReasonCodeId { get; set; }
    public AndonSignalStatus Status { get; set; } = AndonSignalStatus.Active;
    public DateTime RaisedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Notes { get; set; }
    public Guid? RaisedByOperatorId { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
}
