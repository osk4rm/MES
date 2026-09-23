using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Save;
using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar;

/// <summary>
/// Validates weekly calendar entries for a single <see cref="DayOfWeek"/>.
/// An entry with <c>EndTime &lt;= StartTime</c> crosses midnight and is treated
/// as two segments (<c>[Start, 24:00)</c> and <c>[00:00, End)</c>); two entries
/// on the same day overlap when any of their segments intersect.
/// </summary>
internal static class WorkCenterCalendarOverlapValidator
{
    private const long DayTicks = TimeSpan.TicksPerDay;

    public static void ThrowIfOverlapping(IReadOnlyList<WorkCenterCalendarEntryInput> entries)
    {
        foreach (var group in entries.GroupBy(x => x.DayOfWeek))
        {
            var segments = group.Select(ToSegments).ToList();
            for (var i = 0; i < segments.Count; i++)
            for (var j = i + 1; j < segments.Count; j++)
            {
                if (segments[i].Any(s1 => segments[j].Any(s2 => Intersect(s1, s2))))
                    throw new ValidationException(
                        nameof(SaveWorkCenterCalendarRequest.Entries),
                        $"Calendar entries overlap on {group.Key} ({Format(group.ElementAt(i))} intersects {Format(group.ElementAt(j))}).");
            }
        }
    }

    private static IReadOnlyList<(long Start, long End)> ToSegments(WorkCenterCalendarEntryInput entry)
    {
        var start = entry.StartTime.ToTimeSpan().Ticks;
        var end = entry.EndTime.ToTimeSpan().Ticks;

        // End <= Start crosses midnight (End == Start covers the whole day).
        return end > start
            ? [(start, end)]
            : [(start, DayTicks), (0, end)];
    }

    private static bool Intersect((long Start, long End) a, (long Start, long End) b)
        => Math.Max(a.Start, b.Start) < Math.Min(a.End, b.End);

    private static string Format(WorkCenterCalendarEntryInput entry)
        => $"{entry.StartTime:HH\\:mm}-{entry.EndTime:HH\\:mm}";
}
