using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Pure capacity-leveling check for Gantt reschedule moves (issue #305,
/// slice 2/3). Finds the persisted <c>ScheduledOperation</c> overrides on the
/// target Work Center whose windows intersect the moved window. Intervals are
/// half-open <c>[start, end)</c>: bars that only touch at an edge do not
/// conflict. The segment being moved (same order + operation) is excluded so
/// re-saving an unchanged placement never conflicts with itself. No database,
/// no clock — the handler owns all I/O so this class stays trivially
/// unit-testable.
/// </summary>
public static class GanttLeveling
{
    /// <summary>
    /// Returns the overrides on the target machine overlapping
    /// <c>[start, end)</c>, excluding the moved segment itself.
    /// </summary>
    public static IReadOnlyList<ScheduledOperation> FindConflicts(
        IEnumerable<ScheduledOperation> overridesOnMachine,
        DateTime start,
        DateTime end,
        Guid excludeProductionOrderId,
        Guid excludeOperationNodeId)
    {
        return overridesOnMachine
            .Where(o => !(o.ProductionOrderId == excludeProductionOrderId
                && o.OperationNodeId == excludeOperationNodeId)
                && o.PlannedStart < end
                && start < o.PlannedEnd)
            .OrderBy(o => o.PlannedStart)
            .ThenBy(o => o.ProductionOrderId)
            .ThenBy(o => o.OperationNodeId)
            .ToList();
    }
}
