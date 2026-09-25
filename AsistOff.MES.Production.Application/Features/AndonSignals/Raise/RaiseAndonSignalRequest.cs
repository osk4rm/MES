using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Raise;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record RaiseAndonSignalRequest(
    Guid MachineId,
    AndonSignalCategory Category,
    Guid? ReasonCodeId,
    DateTime RaisedAt,
    string? Notes,
    Guid? RaisedByOperatorId,
    Guid? ProductionOrderId) : ITenantRequest<AndonSignalResponse>;
