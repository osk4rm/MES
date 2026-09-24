using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags;

/// <summary>Shared validation for tag create/update so both paths enforce the same rules.</summary>
internal static class MachineTelemetryTagRules
{
    public const int MaxNodeIdLength = 256;
    public const int MaxDisplayNameLength = 200;
    public const int MinPollIntervalSeconds = 1;
    public const int MaxPollIntervalSeconds = 3600;

    public static void Validate(
        Guid machineId,
        string nodeId,
        string displayName,
        TelemetryDataType dataType,
        int pollIntervalSeconds)
    {
        if (machineId == Guid.Empty)
            throw new ValidationException(nameof(machineId), "Machine is required.");
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ValidationException(nameof(nodeId), "NodeId is required.");
        if (nodeId.Trim().Length > MaxNodeIdLength)
            throw new ValidationException(nameof(nodeId), $"NodeId must be at most {MaxNodeIdLength} characters.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ValidationException(nameof(displayName), "Display name is required.");
        if (displayName.Trim().Length > MaxDisplayNameLength)
            throw new ValidationException(nameof(displayName), $"Display name must be at most {MaxDisplayNameLength} characters.");
        if (!Enum.IsDefined(dataType))
            throw new ValidationException(nameof(dataType), "Data type is not supported.");
        if (pollIntervalSeconds is < MinPollIntervalSeconds or > MaxPollIntervalSeconds)
            throw new ValidationException(nameof(pollIntervalSeconds), $"Poll interval must be between {MinPollIntervalSeconds} and {MaxPollIntervalSeconds} seconds.");
    }
}
