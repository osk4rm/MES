using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections;

/// <summary>
/// Read model for one OPC UA connection registry row. <c>LastError</c>
/// carries the raw provider message only for callers holding
/// <c>production.write</c>; read-only callers receive <c>null</c> (issue
/// #372) while liveness fields (<c>IsEnabled</c>, <c>LastSeenAtUtc</c>) stay
/// visible to everyone.
/// </summary>
public record OpcUaConnectionResponse(
    Guid Id,
    Guid MachineId,
    string EndpointUrl,
    OpcUaSecurityPolicy SecurityPolicy,
    int PollIntervalSeconds,
    bool IsEnabled,
    DateTime? LastSeenAtUtc,
    string? LastError,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
