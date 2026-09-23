using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class WorkCenterCalendarsRepository(DefaultContext context) : IWorkCenterCalendarsRepository
{
    public async Task<WorkCenterCalendar?> GetByMachineIdAsync(Guid machineId, CancellationToken cancellationToken = default)
    {
        return await context.Set<WorkCenterCalendar>()
            .Include(x => x.Entries).ThenInclude(x => x.Shift)
            .FirstOrDefaultAsync(x => x.MachineId == machineId, cancellationToken);
    }

    public async Task<WorkCenterCalendar> AddAsync(WorkCenterCalendar calendar, CancellationToken cancellationToken = default)
    {
        context.Set<WorkCenterCalendar>().Add(calendar);
        await context.SaveChangesAsync(cancellationToken);
        return calendar;
    }

    public async Task ReplaceEntriesAsync(Guid calendarId, IReadOnlyCollection<WorkCenterCalendarEntry> entries, CancellationToken cancellationToken = default)
    {
        var existing = await context.Set<WorkCenterCalendarEntry>()
            .Where(x => x.CalendarId == calendarId)
            .ToListAsync(cancellationToken);
        context.Set<WorkCenterCalendarEntry>().RemoveRange(existing);
        foreach (var entry in entries)
            context.Set<WorkCenterCalendarEntry>().Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }
}
