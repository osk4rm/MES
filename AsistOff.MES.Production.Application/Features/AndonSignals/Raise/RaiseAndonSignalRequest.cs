using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Raise;

public record RaiseAndonSignalRequest(
    Guid MachineId,
    AndonSignalCategory Category,
    Guid? ReasonCodeId,
    DateTime RaisedAt,
    string? Notes,
    Guid? RaisedByOperatorId,
    Guid? ProductionOrderId) : ITenantRequest<AndonSignalResponse>;
