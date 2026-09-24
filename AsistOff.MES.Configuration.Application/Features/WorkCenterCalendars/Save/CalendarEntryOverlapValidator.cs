using AsistOff.MES.Shared.Abstractions.Exceptions;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Save;

/// <summary>
/// Overlap detection for weekly calendar entries.
///
/// Every entry is projected onto a linear one-week timeline (seconds since
/// Sunday 00:00) as a half-open <c>[start, end)</c> interval. An entry whose
/// <c>EndTime</c> is not after its <c>StartTime</c> crosses midnight, so its
/// end is pushed into the following day — and, when it wraps past the end of
/// the week, split into two segments. Two entries are considered overlapping
/// when any of their segments intersect, which also catches an overnight
/// window that runs into the next day's first window.
/// </summary>
internal static class CalendarEntryOverlapValidator
{
    private const int SecondsPerDay = 86_400;
    private const int SecondsPerWeek = 7 * SecondsPerDay;

    /// <summary>
    /// Throws <see cref="ValidationException"/> when two entries share any point
    /// in time. The check is deliberately week-wide: an entry that crosses
    /// midnight is compared against the following day's entries as well.
    /// </summary>
    internal static void EnsureNoOverlaps(IReadOnlyList<WorkCenterCalendarEntryRequest> entries)
    {
        var segments = entries.Select(e => Expand(e.DayOfWeek, e.StartTime, e.EndTime)).ToList();

        for (var i = 0; i < segments.Count; i++)
        {
            for (var j = i + 1; j < segments.Count; j++)
            {
                if (Intersects(segments[i], segments[j]))
                {
                    throw new ValidationException(
                        nameof(SaveWorkCenterCalendarRequest.Entries),
                        $"Calendar entries {Describe(entries[i])} and {Describe(entries[j])} overlap.");
                }
            }
        }
    }

    private static string Describe(WorkCenterCalendarEntryRequest entry) =>
        $"[{entry.DayOfWeek} {entry.StartTime:HH\\:mm}-{entry.EndTime:HH\\:mm}]";

    private static List<(int Start, int End)> Expand(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        var dayIndex = (int)day;
        var startSeconds = (int)start.ToTimeSpan().TotalSeconds;
        var endSeconds = (int)end.ToTimeSpan().TotalSeconds;

        var absoluteStart = dayIndex * SecondsPerDay + startSeconds;
        var duration = endSeconds - startSeconds;
        if (duration <= 0)
        {
            // EndTime <= StartTime — the window crosses midnight.
            duration += SecondsPerDay;
        }

        var absoluteEnd = absoluteStart + duration;
        if (absoluteEnd <= SecondsPerWeek)
        {
            return [(absoluteStart, absoluteEnd)];
        }

        return [(absoluteStart, SecondsPerWeek), (0, absoluteEnd - SecondsPerWeek)];
    }

    private static bool Intersects(
        IReadOnlyList<(int Start, int End)> left,
        IReadOnlyList<(int Start, int End)> right)
    {
        foreach (var a in left)
        {
            foreach (var b in right)
            {
                if (a.Start < b.End && b.Start < a.End)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
