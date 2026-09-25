using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Shift-aware dispatch board: one bucket per day carrying the active shifts
/// with their roster headcounts, plus the ordered Released/InProgress order
/// rows. Computed read-only from existing tables; no new entities.
/// </summary>
public sealed record DispatchBoardResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<DispatchDayResponse> Days,
    IReadOnlyList<DispatchOrderRowResponse> Orders);

/// <summary>One calendar day of the requested window with its active shifts.</summary>
public sealed record DispatchDayResponse(
    DateOnly Date,
    IReadOnlyList<DispatchShiftResponse> Shifts);

/// <summary>An active shift with the roster headcount for the bucket date.</summary>
public sealed record DispatchShiftResponse(
    Guid ShiftId,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsOvernight,
    int Headcount);

/// <summary>
/// A Released or InProgress order that is overdue, due inside the window or
/// has no due date, with read-time confirmation totals reused from
/// <c>ProductionOrderMappers</c>.
/// </summary>
public sealed record DispatchOrderRowResponse(
    Guid Id,
    string Code,
    Guid ProductId,
    decimal PlannedQuantity,
    decimal ProducedQuantity,
    decimal ScrappedQuantity,
    decimal RemainingQuantity,
    int Priority,
    DateTime? DueDate,
    ProductionOrderStatus Status,
    bool IsOverdue);
