using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// Dictionary entry for one OPC UA telemetry signal exposed by a Work Center
/// (stored as <see cref="MachineId"/>) via PLC/SCADA. Tags are polled by the
/// telemetry poller (a later increment); readings are appended to
/// <see cref="TelemetryReading"/> and the tag dictionary itself changes rarely.
/// <see cref="NodeId"/> is unique per (tenant, machine).
/// </summary>
public class MachineTelemetryTag : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Work Center exposing the signal (loose reference to Configuration Machines).</summary>
    public Guid MachineId { get; set; }

    /// <summary>OPC UA node identifier, e.g. <c>ns=2;s=Line1.Oven.Temperature</c>.</summary>
    public required string NodeId { get; set; }

    public required string DisplayName { get; set; }

    public TelemetryDataType DataType { get; set; } = TelemetryDataType.Double;

    /// <summary>Poll interval in seconds, 1..3600. Consumed by the poller increment.</summary>
    public int PollIntervalSeconds { get; set; } = 30;

    public bool IsEnabled { get; set; } = true;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
}
