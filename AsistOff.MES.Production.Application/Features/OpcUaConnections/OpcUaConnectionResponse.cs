using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections;

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
