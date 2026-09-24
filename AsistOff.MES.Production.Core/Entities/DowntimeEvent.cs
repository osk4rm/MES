using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A downtime event recorded against a Work Center (Machine), classified by a
/// reason code. Downtime is a property of the Work Center, independent of any
/// production order — <see cref="ProductionOrderId"/> is reserved for a future
/// increment and is never populated here.
/// </summary>
public class DowntimeEvent : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Work Center the downtime applies to (loose reference to Configuration Machines).</summary>
    public Guid MachineId { get; set; }

    /// <summary>Classifying reason code (loose reference to Configuration ReasonCodes).</summary>
    public Guid ReasonCodeId { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Null while the event is still open.</summary>
    public DateTime? EndedAt { get; set; }

    public string? Notes { get; set; }

    public Guid? ReportedByOperatorId { get; set; }

    /// <summary>Reserved for a future increment — never set in this increment.</summary>
    public Guid? ProductionOrderId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Derived from <see cref="EndedAt"/>, never persisted.</summary>
    public DowntimeEventStatus Status => EndedAt.HasValue ? DowntimeEventStatus.Closed : DowntimeEventStatus.Open;

    /// <summary>Total minutes between start and end; null while the event is open.</summary>
    public double? DurationMinutes => EndedAt.HasValue
        ? (EndedAt.Value - StartedAt).TotalMinutes
        : null;
}
