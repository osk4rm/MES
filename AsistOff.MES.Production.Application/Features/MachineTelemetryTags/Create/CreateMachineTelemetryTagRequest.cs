using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Create;

public record CreateMachineTelemetryTagRequest(
    Guid MachineId,
    string NodeId,
    string DisplayName,
    TelemetryDataType DataType,
    int PollIntervalSeconds,
    string? Description) : ITenantRequest<MachineTelemetryTagResponse>;
