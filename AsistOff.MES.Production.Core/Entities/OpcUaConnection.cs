using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// Tenant-scoped registry entry for one OPC UA server endpoint exposing a
/// Work Center (stored as <see cref="MachineId"/>) via PLC/SCADA. The
/// background <c>OpcUaPollingService</c> follows <see cref="IsEnabled"/> and
/// writes <see cref="TelemetryReading"/> rows through the shared ingestion
/// service; health is tracked via <see cref="LastSeenAtUtc"/> and
/// <see cref="LastError"/>. (Tenant, machine, endpoint) is unique.
/// <see cref="MachineId"/> is a loose reference to Configuration Machines,
/// following the DowntimeEvent pattern — existence validation is out of
/// scope and the UI only offers current-tenant machines.
/// </summary>
public class OpcUaConnection : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Work Center exposed through this endpoint (loose reference to Configuration Machines).</summary>
    public Guid MachineId { get; set; }

    /// <summary>Absolute OPC UA server endpoint URL (http, https, opc.tcp or opc.http scheme).</summary>
    public required string EndpointUrl { get; set; }

    public OpcUaSecurityPolicy SecurityPolicy { get; set; } = OpcUaSecurityPolicy.None;

    /// <summary>Poll cadence in seconds, 5..3600. Stored in this slice; scheduling follows in a later increment.</summary>
    public int PollIntervalSeconds { get; set; } = 30;

    public bool IsEnabled { get; set; } = true;

    /// <summary>Last successful poll timestamp (UTC), null until the first success.</summary>
    public DateTime? LastSeenAtUtc { get; set; }

    /// <summary>Last poll failure message (max 512), cleared on success.</summary>
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
