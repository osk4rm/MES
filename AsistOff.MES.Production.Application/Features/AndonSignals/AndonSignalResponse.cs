using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.AndonSignals;

public record AndonSignalResponse(
    Guid Id,
    Guid MachineId,
    AndonSignalCategory Category,
    Guid? ReasonCodeId,
    AndonSignalStatus Status,
    DateTime RaisedAt,
    DateTime? AcknowledgedAt,
    DateTime? ResolvedAt,
    string? Notes,
    Guid? RaisedByOperatorId,
    Guid? ProductionOrderId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
