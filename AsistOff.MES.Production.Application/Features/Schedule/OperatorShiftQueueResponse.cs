using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Operator shift queue: the current-shift work list for one operator code.
/// Shift context carries the covering roster assignment (null when the
/// operator has no assignment covering now); the queue holds Released and
/// InProgress Production Orders overlapping the shift window ordered next-up
/// (priority, then due date); signals hold the open Andon conditions for the
/// queued Work Centers. Computed read-only from existing tables; no migration.
/// </summary>
public sealed record OperatorShiftQueueResponse(
    string OperatorCode,
    Guid OperatorId,
    OperatorShiftContextResponse? Shift,
    IReadOnlyList<OperatorShiftQueuedOrderResponse> Orders,
    IReadOnlyList<OperatorShiftQueueSignalResponse> ActiveSignals);

/// <summary>
/// The roster assignment covering now, with the shift window in UTC.
/// Overnight shifts (end not after start) end on the day after <see cref="Date"/>.
/// </summary>
public sealed record OperatorShiftContextResponse(
    Guid ShiftId,
    string ShiftCode,
    string ShiftName,
    DateOnly Date,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    bool IsOvernight);

/// <summary>
/// A Released or InProgress order overlapping the shift window, with
/// read-time confirmation totals. <c>ProductCode</c> is resolved read-time and
/// is null when the product row is absent. ProductionOrder carries no Work
/// Center FK, so <c>MachineId</c> is the scheduled Work Center from the manual
/// <c>ScheduledOperation</c> override when one exists, otherwise null
/// (unscheduled, same as the Gantt unassigned lane).
/// <c>NoQualifiedOperator</c> is true when the order's recipe operations
/// require at least one skill and zero operators assigned to the shift hold
/// every required skill (issue #397).
/// </summary>
public sealed record OperatorShiftQueuedOrderResponse(
    Guid Id,
    string Code,
    Guid ProductId,
    string? ProductCode,
    decimal PlannedQuantity,
    decimal ProducedQuantity,
    decimal ScrappedQuantity,
    decimal RemainingQuantity,
    Guid? MachineId,
    string? MachineCode,
    string? MachineName,
    int Priority,
    DateTime? DueDate,
    ProductionOrderStatus Status,
    bool NoQualifiedOperator = false);

/// <summary>
/// An open (Active) Andon signal for one of the queued Work Centers. The
/// signal table carries no severity column: <c>Severity</c> is the category
/// name, matching the shift handover context read-model.
/// </summary>
public sealed record OperatorShiftQueueSignalResponse(
    Guid Id,
    Guid MachineId,
    string? MachineCode,
    AndonSignalCategory Category,
    string Severity,
    DateTime RaisedAt);
