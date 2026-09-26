namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// The persisted manual placement of one Gantt segment after a reschedule
/// move (issue #305, slice 2/3). <see cref="ShiftCoverageWarning"/> is
/// advisory: the new window spans dates with zero active shifts or zero
/// roster headcount. <see cref="ConflictingSegmentIds"/> lists the persisted
/// overrides the move overlapped and bypassed via <c>force</c>; it is empty
/// on a clean move (a non-forced overlap is a 409, not a response).
/// </summary>
public sealed record RescheduleGanttSegmentResponse(
    Guid Id,
    Guid ProductionOrderId,
    Guid OperationNodeId,
    Guid MachineId,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    bool ShiftCoverageWarning,
    IReadOnlyList<Guid> ConflictingSegmentIds);
