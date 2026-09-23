namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Responses;

public record WorkCenterCalendarEntryResponse(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? ShiftId,
    string? ShiftCode,
    string? ShiftName,
    bool IsWorking);

public record WorkCenterCalendarResponse(
    Guid Id,
    Guid MachineId,
    IReadOnlyList<WorkCenterCalendarEntryResponse> Entries);
