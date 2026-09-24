using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Status;

/// <summary>
/// Per-tag connection-status readout: last seen reading, readings in the
/// last hour and a stale flag (no reading within 2x the simulator poll
/// interval). Tags that never reported have a <c>null</c> LastReadAt.
/// </summary>
public record TelemetryTagStatusEntry(
    Guid TagId,
    Guid MachineId,
    string NodeId,
    string DisplayName,
    bool IsEnabled,
    DateTime? LastReadAt,
    int ReadingsLastHour,
    bool Stale);

/// <summary>
/// Connection status for every tag of the caller tenant, plus the simulator
/// master switch from configuration (drives the frontend on/off indicator).
/// </summary>
public record TelemetryStatusResponse(
    bool SimulatorEnabled,
    int SimulatorIntervalSeconds,
    IReadOnlyCollection<TelemetryTagStatusEntry> Tags);

/// <summary>Tenant-scoped connection status for the caller tenant's tags.</summary>
public record GetTelemetryStatusRequest : ITenantRequest<TelemetryStatusResponse>;
