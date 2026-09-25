using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Start;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record StartDowntimeEventRequest(
    Guid MachineId,
    Guid ReasonCodeId,
    DateTime StartedAt,
    string? Notes,
    Guid? ReportedByOperatorId,
    Guid? ProductionOrderId = null) : ITenantRequest<DowntimeEventResponse>;
