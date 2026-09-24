using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents;

public record DowntimeEventResponse(
    Guid Id,
    Guid MachineId,
    Guid ReasonCodeId,
    DateTime StartedAt,
    DateTime? EndedAt,
    DowntimeEventStatus Status,
    double? DurationMinutes,
    string? Notes,
    Guid? ReportedByOperatorId,
    Guid? ProductionOrderId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
