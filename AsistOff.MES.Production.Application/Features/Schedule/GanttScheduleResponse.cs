namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Time-phased Gantt schedule: computed operation segments grouped per
/// Work Center (Machine). Slice 1/3 (issue #304) is a read-only computed
/// read-model with persisted <c>ScheduledOperation</c> overrides overlaid;
/// rescheduling writes and the UI arrive in slices 2/3 and 3/3.
/// </summary>
public sealed record GanttScheduleResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<GanttMachineGroupResponse> Groups);

/// <summary>One Work Center lane of the Gantt chart with its operation bars.</summary>
public sealed record GanttMachineGroupResponse(
    Guid? MachineId,
    string? MachineCode,
    string? MachineName,
    IReadOnlyList<GanttBarResponse> Bars);

/// <summary>
/// One scheduled operation segment: which order and operation runs on the
/// Work Center, when, and whether it ends after the order due date.
/// </summary>
public sealed record GanttBarResponse(
    Guid ProductionOrderId,
    string ProductionOrderCode,
    Guid OperationNodeId,
    string OperationCode,
    string OperationName,
    Guid? MachineId,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    bool IsOverdue);
