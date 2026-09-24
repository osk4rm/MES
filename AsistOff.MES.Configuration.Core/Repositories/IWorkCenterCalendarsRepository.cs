using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IWorkCenterCalendarsRepository
{
    /// <summary>Returns the calendar of the given Work Center together with its weekly entries.</summary>
    Task<WorkCenterCalendar?> GetByMachineIdAsync(Guid machineId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a brand new calendar (with its entries) in a single transaction.</summary>
    Task<WorkCenterCalendar> CreateAsync(
        WorkCenterCalendar calendar,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces every entry of an existing calendar with the supplied
    /// collection: the old entries are deleted and the new ones inserted in one
    /// <c>SaveChanges</c> call.
    /// </summary>
    Task ReplaceEntriesAsync(
        Guid calendarId,
        IReadOnlyCollection<WorkCenterCalendarEntry> entries,
        CancellationToken cancellationToken = default);
}
