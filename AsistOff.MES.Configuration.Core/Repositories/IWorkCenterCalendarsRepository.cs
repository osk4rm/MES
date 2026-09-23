using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IWorkCenterCalendarsRepository
{
    Task<WorkCenterCalendar?> GetByMachineIdAsync(Guid machineId, CancellationToken cancellationToken = default);
    Task<WorkCenterCalendar> AddAsync(WorkCenterCalendar calendar, CancellationToken cancellationToken = default);
    Task ReplaceEntriesAsync(Guid calendarId, IReadOnlyCollection<WorkCenterCalendarEntry> entries, CancellationToken cancellationToken = default);
}
