using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class WorkCenterCalendarsRepository(DefaultContext context) : IWorkCenterCalendarsRepository
{
    public async Task<WorkCenterCalendar?> GetByMachineIdAsync(
        Guid machineId, CancellationToken cancellationToken = default)
    {
        return await context.Set<WorkCenterCalendar>()
            .Include(x => x.Entries)
            .FirstOrDefaultAsync(x => x.MachineId == machineId, cancellationToken);
    }

    public async Task<WorkCenterCalendar> CreateAsync(
        WorkCenterCalendar calendar, CancellationToken cancellationToken = default)
    {
        context.Set<WorkCenterCalendar>().Add(calendar);
        await context.SaveChangesAsync(cancellationToken);
        return calendar;
    }

    public async Task ReplaceEntriesAsync(
        Guid calendarId,
        IReadOnlyCollection<WorkCenterCalendarEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var calendar = await context.Set<WorkCenterCalendar>()
            .Include(x => x.Entries)
            .FirstAsync(x => x.Id == calendarId, cancellationToken);

        // Delete + insert are flushed in a single SaveChanges, i.e. one
        // transaction, so a failed validation can never leave a half-written week.
        context.Set<WorkCenterCalendarEntry>().RemoveRange(calendar.Entries);
        calendar.Entries.Clear();

        foreach (var entry in entries)
        {
            entry.WorkCenterCalendarId = calendarId;

            // Entries arrive with a pre-assigned key, which makes EF treat them
            // as existing rows when they are only reachable through the parent
            // navigation - adding them explicitly forces an INSERT.
            context.Set<WorkCenterCalendarEntry>().Add(entry);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
