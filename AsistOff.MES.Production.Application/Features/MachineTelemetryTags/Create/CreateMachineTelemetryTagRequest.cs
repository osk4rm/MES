using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Create;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record CreateMachineTelemetryTagRequest(
    Guid MachineId,
    string NodeId,
    string DisplayName,
    TelemetryDataType DataType,
    int PollIntervalSeconds,
    string? Description) : ITenantRequest<MachineTelemetryTagResponse>;
