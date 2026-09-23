namespace AsistOff.MES.Integration.Tests.TestData;

/// <summary>Shape of a reason code as returned by <c>/api/reason-codes</c>.</summary>
public sealed record ReasonCodeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    short Category,
    bool IsActive,
    int SortIndex);

/// <summary>Shape of the paged responses returned by browse endpoints.</summary>
public sealed record PagedResponseDto<T>(
    int TotalCount,
    int TotalPages,
    IReadOnlyCollection<T> Items);

/// <summary>Shape of a shift as returned by <c>/api/shifts</c>.</summary>
public sealed record ShiftDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive);

/// <summary>Shape of a machine as returned by <c>/api/machines</c>.</summary>
public sealed record MachineDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    string? SyncId);

/// <summary>Shape of a calendar entry as returned by <c>/api/machines/{id}/calendar</c>.</summary>
public sealed record CalendarEntryDto(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? ShiftId,
    string? ShiftCode,
    string? ShiftName,
    bool IsWorking);

/// <summary>Shape of a work-center calendar as returned by <c>/api/machines/{id}/calendar</c>.</summary>
public sealed record WorkCenterCalendarDto(
    Guid Id,
    Guid MachineId,
    IReadOnlyList<CalendarEntryDto> Entries);
