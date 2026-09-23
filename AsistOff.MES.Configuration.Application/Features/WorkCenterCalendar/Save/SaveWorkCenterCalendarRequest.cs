using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Save;

public record WorkCenterCalendarEntryInput(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? ShiftId,
    bool IsWorking);

public record SaveWorkCenterCalendarRequest : ITenantRequest<WorkCenterCalendarResponse>
{
    public Guid MachineId { get; init; }
    public IReadOnlyList<WorkCenterCalendarEntryInput> Entries { get; init; } = [];
}
