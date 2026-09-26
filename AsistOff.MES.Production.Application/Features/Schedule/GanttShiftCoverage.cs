using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Pure shift-coverage check for Gantt reschedule moves (issue #305,
/// slice 2/3). Mirrors the dispatch board's day-bucket aggregation (active
/// shifts with distinct-operator roster headcounts per date) and raises an
/// advisory warn flag when the moved window spans dates with zero active
/// shifts or zero roster headcount. Advisory only: it never blocks the move.
/// No database — the handler owns all I/O so this class stays trivially
/// unit-testable.
/// </summary>
public static class GanttShiftCoverage
{
    /// <summary>
    /// Returns <c>true</c> when the planner should be warned: there are no
    /// active shifts at all, or the roster carries zero headcount across every
    /// date spanned by <c>[startDate, endDate]</c>.
    /// </summary>
    public static bool HasCoverageWarning(
        IReadOnlyCollection<Shift> activeShifts,
        IReadOnlyCollection<OperatorShiftAssignment> rosterInSpan,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (activeShifts.Count == 0)
            return true;

        var headcount = rosterInSpan
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .GroupBy(r => (r.Date, r.ShiftId))
            .Sum(g => g.Select(r => r.OperatorId).Distinct().Count());

        return headcount == 0;
    }
}
