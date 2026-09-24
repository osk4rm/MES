using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Status;

/// <summary>
/// Health of one OPC UA connection: the <c>IsLive</c> flag follows
/// <see cref="OpcUaConnectionHealth"/> (enabled plus a poll within twice the
/// poll interval). Tag counts cover the connection's Work Center
/// (<c>MachineId</c>): <c>TotalTags</c> counts every tag, <c>ReportingTags</c>
/// counts tags with a reading inside their own 2x threshold, and
/// <c>StaleTags</c> is the remainder (including tags that never reported).
/// A never-polled connection (<c>LastSeenAtUtc</c> null) is stale, never live.
/// </summary>
public record OpcUaConnectionStatusEntry(
    Guid ConnectionId,
    Guid MachineId,
    string EndpointUrl,
    bool IsEnabled,
    DateTime? LastSeenAtUtc,
    string? LastError,
    bool IsLive,
    int TotalTags,
    int ReportingTags,
    int StaleTags);

/// <summary>
/// Status of every OPC UA connection of the caller tenant in stable
/// (machine, endpoint) order. Disabled connections are not applicable and
/// excluded from <c>LiveCount</c>; enabled connections without a live poll
/// are counted in <c>StaleCount</c>.
/// </summary>
public record OpcUaConnectionStatusResponse(
    IReadOnlyCollection<OpcUaConnectionStatusEntry> Connections,
    int TotalCount,
    int LiveCount,
    int StaleCount,
    int DisabledCount);

/// <summary>
/// Tenant-scoped OPC UA connection status, optionally filtered to one Work
/// Center. An unknown or cross-tenant machine id yields
/// <c>NotFoundException</c> (404).
/// </summary>
public record GetOpcUaConnectionStatusRequest : ITenantRequest<OpcUaConnectionStatusResponse>
{
    public Guid? MachineId { get; set; }
}
