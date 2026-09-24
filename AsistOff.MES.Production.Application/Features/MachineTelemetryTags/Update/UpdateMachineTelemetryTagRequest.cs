using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Update;

/// <summary>
/// Updates tag dictionary fields. <c>MachineId</c>/<c>NodeId</c> are immutable
/// after create so the (tenant, machine, node) uniqueness can never break.
/// </summary>
public record UpdateMachineTelemetryTagRequest(
    Guid Id,
    string DisplayName,
    TelemetryDataType DataType,
    int PollIntervalSeconds,
    bool IsEnabled,
    string? Description) : ITenantRequest;
