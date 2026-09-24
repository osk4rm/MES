using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags;

public record MachineTelemetryTagResponse(
    Guid Id,
    Guid MachineId,
    string NodeId,
    string DisplayName,
    TelemetryDataType DataType,
    int PollIntervalSeconds,
    bool IsEnabled,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
