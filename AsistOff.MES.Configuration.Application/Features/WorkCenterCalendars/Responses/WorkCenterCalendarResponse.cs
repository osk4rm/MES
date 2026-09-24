namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Responses;

public record WorkCenterCalendarEntryResponse(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? ShiftId,
    bool IsWorking);

public record WorkCenterCalendarResponse(
    Guid Id,
    Guid MachineId,
    string MachineCode,
    string MachineName,
    IReadOnlyCollection<WorkCenterCalendarEntryResponse> Entries);
