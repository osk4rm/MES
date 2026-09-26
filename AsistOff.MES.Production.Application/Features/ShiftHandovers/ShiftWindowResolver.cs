using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers;

/// <summary>
/// Resolves the calendar entry covering an instant. Entries are half-open
/// <c>[StartTime, EndTime)</c>; when <c>EndTime</c> is not after
/// <c>StartTime</c> the entry crosses midnight into the following day, so the
/// previous day's overnight entries are candidates too.
/// </summary>
internal static class ShiftWindowResolver
{
    public static WorkCenterCalendarEntry? FindCoveringEntry(
        IEnumerable<WorkCenterCalendarEntry> entries, DateTime instantUtc)
    {
        var time = TimeOnly.FromDateTime(instantUtc);
        var day = instantUtc.DayOfWeek;
        var previousDay = day == DayOfWeek.Sunday ? DayOfWeek.Saturday : day - 1;

        foreach (var entry in entries)
        {
            var overnight = entry.EndTime <= entry.StartTime;

            if (!overnight)
            {
                if (entry.DayOfWeek == day && time >= entry.StartTime && time < entry.EndTime)
                    return entry;
            }
            else if ((entry.DayOfWeek == day && time >= entry.StartTime)
                || (entry.DayOfWeek == previousDay && time < entry.EndTime))
            {
                return entry;
            }
        }

        return null;
    }
}
