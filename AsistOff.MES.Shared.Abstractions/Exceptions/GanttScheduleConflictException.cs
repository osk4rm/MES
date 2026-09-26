namespace AsistOff.MES.Shared.Abstractions.Exceptions;

/// <summary>
/// Capacity-leveling conflict for a Gantt reschedule move (issue #305): the
/// moved window overlaps persisted overrides on the same Work Center and the
/// caller did not set <c>force</c>. Carries the conflicting
/// <c>ScheduledOperation</c> ids so the planner (and slice 3/3 UI) can
/// highlight them. Maps to HTTP 409 with a <c>conflictingSegmentIds</c>
/// problem-details extension. Retry: narrow the window, pick another Work
/// Center, or resubmit with <c>force</c> to persist anyway.
/// </summary>
public class GanttScheduleConflictException : ConflictException
{
    public IReadOnlyList<Guid> ConflictingSegmentIds { get; }

    public GanttScheduleConflictException(IReadOnlyList<Guid> conflictingSegmentIds)
        : base($"Move overlaps {conflictingSegmentIds.Count} scheduled segment(s) on the same Work Center: {string.Join(", ", conflictingSegmentIds)}. Narrow the window, pick another Work Center, or resubmit with force to persist anyway.")
    {
        ConflictingSegmentIds = conflictingSegmentIds;
    }
}
