using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;

namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Pure domain scheduler for the Gantt read-model (issue #304, slice 1/3).
/// Explodes one Production Order into operation segments from
/// <see cref="OperationNode"/> timing, chained by
/// <see cref="OperationDependency"/> edges. No database, no clock, no
/// tenant context — the handler owns all I/O so this class stays trivially
/// unit-testable.
/// </summary>
public static class GanttScheduler
{
    /// <summary>
    /// One computed segment: the operation, its duration source values
    /// resolved to minutes, the preferred Work Center and the planned window.
    /// </summary>
    public sealed record Segment(
        Guid OperationNodeId,
        string OperationCode,
        string OperationName,
        Guid? MachineId,
        DateTime PlannedStart,
        DateTime PlannedEnd);

    /// <summary>
    /// Resolves the total minutes of one operation for the given planned
    /// quantity: setup + run + teardown + queue. Per-unit run time scales
    /// with the quantity (<c>RunTimePerUnitSeconds × quantity / 60</c>);
    /// per-batch run time is a flat <c>RunTimePerBatchMinutes</c> block.
    /// Missing components count as zero; negative totals clamp to zero.
    /// </summary>
    public static decimal ComputeDurationMinutes(OperationNode node, decimal plannedQuantity)
    {
        var setup = node.SetupTimeMinutes ?? 0m;
        var teardown = node.TeardownTimeMinutes ?? 0m;
        var queue = node.QueueTimeMinutes ?? 0m;

        decimal run = node.RunTimeMode == RunTimeMode.PerBatchMinutes
            ? node.RunTimePerBatchMinutes ?? 0m
            : (node.RunTimePerUnitSeconds ?? 0m) * plannedQuantity / 60m;

        var total = setup + run + teardown + queue;
        return total < 0m ? 0m : total;
    }

    /// <summary>
    /// Preferred Work Center for an operation: the first resource requirement
    /// carrying a <c>PreferredMachineId</c>, or null when the recipe states
    /// no machine preference (the bar lands in the unassigned lane).
    /// </summary>
    public static Guid? ResolveMachineId(OperationNode node)
        => node.ResourceRequirements?.FirstOrDefault(r => r.PreferredMachineId.HasValue)?.PreferredMachineId;

    /// <summary>
    /// Lays out every operation of one order as segments. Nodes chain by
    /// dependency edges: a FinishToStart edge starts the successor at
    /// <c>predecessor end + LagMinutes</c>; any other edge type falls back
    /// to plain sequential chaining (<c>predecessor end</c>, lag ignored).
    /// Nodes without predecessors start at the anchor; independent roots run
    /// in parallel. The whole chain anchors backward from
    /// <paramref name="order"/>'s due date (last segment ends exactly at the
    /// due date) or forward from <paramref name="anchorNowUtc"/> when the
    /// order has no due date.
    /// </summary>
    public static IReadOnlyList<Segment> ComputeOrderSegments(
        ProductionOrder order,
        IReadOnlyCollection<OperationNode> nodes,
        DateTime anchorNowUtc)
    {
        if (nodes.Count == 0)
            return [];

        var byId = nodes.ToDictionary(n => n.Id);
        var durations = nodes.ToDictionary(n => n.Id, n => ComputeDurationMinutes(n, order.PlannedQuantity));

        // Successor -> [(predecessor, lag)] using only edges whose endpoints
        // both belong to this version. Non-FinishToStart edges fall back to
        // sequential chaining (lag ignored).
        var predecessors = new Dictionary<Guid, List<(Guid PredecessorId, decimal LagMinutes)>>();
        foreach (var node in nodes)
        {
            var edges = new List<(Guid, decimal)>();
            foreach (var dep in node.Dependencies ?? [])
            {
                if (!byId.ContainsKey(dep.PredecessorOperationNodeId))
                    continue;

                var lag = dep.DependencyType == OperationDependencyType.FinishToStart
                    ? dep.LagMinutes ?? 0m
                    : 0m;
                edges.Add((dep.PredecessorOperationNodeId, lag));
            }

            predecessors[node.Id] = edges;
        }

        var topoOrder = TopologicalOrder(nodes, predecessors);

        // Forward layout in relative minutes from an arbitrary zero.
        var ends = new Dictionary<Guid, decimal>();
        var starts = new Dictionary<Guid, decimal>();
        foreach (var id in topoOrder)
        {
            decimal start = 0m;
            foreach (var (predecessorId, lag) in predecessors[id])
            {
                if (ends.TryGetValue(predecessorId, out var predEnd))
                    start = Math.Max(start, predEnd + lag);
            }

            starts[id] = start;
            ends[id] = start + durations[id];
        }

        var makespan = ends.Count == 0 ? 0m : ends.Values.Max();

        // Anchor: backward from the due date (last segment ends at DueDate)
        // or forward from now when there is no due date.
        var anchor = order.DueDate.HasValue
            ? EnsureUtc(order.DueDate.Value).AddMinutes(-(double)makespan)
            : EnsureUtc(anchorNowUtc);

        var segments = new List<Segment>(nodes.Count);
        foreach (var node in nodes)
        {
            var start = anchor.AddMinutes((double)starts[node.Id]);
            var end = anchor.AddMinutes((double)ends[node.Id]);
            segments.Add(new Segment(
                node.Id,
                node.Code,
                node.Name,
                ResolveMachineId(node),
                start,
                end));
        }

        return segments
            .OrderBy(s => s.PlannedStart)
            .ThenBy(s => s.OperationCode, StringComparer.Ordinal)
            .ToList();
    }

    private static List<Guid> TopologicalOrder(
        IReadOnlyCollection<OperationNode> nodes,
        Dictionary<Guid, List<(Guid PredecessorId, decimal LagMinutes)>> predecessors)
    {
        // Kahn's algorithm with a deterministic SortIndex/Code tie-break.
        // On a cycle (rejected at write time, but cheap to survive here) the
        // leftover nodes fall back to sequential SortIndex order so the board
        // still renders instead of throwing.
        var successors = new Dictionary<Guid, List<Guid>>();
        var indegree = new Dictionary<Guid, int>();
        foreach (var node in nodes)
        {
            successors[node.Id] = [];
            indegree[node.Id] = 0;
        }

        foreach (var (successor, preds) in predecessors)
        {
            foreach (var (predecessor, _) in preds.Distinct())
            {
                if (!successors.ContainsKey(predecessor) || !indegree.ContainsKey(successor))
                    continue;

                successors[predecessor].Add(successor);
                indegree[successor]++;
            }
        }

        var byId = nodes.ToDictionary(n => n.Id);
        var ready = new SortedSet<Guid>(Comparer<Guid>.Create((a, b) =>
        {
            var cmp = byId[a].SortIndex.CompareTo(byId[b].SortIndex);
            if (cmp != 0)
                return cmp;
            cmp = string.Compare(byId[a].Code, byId[b].Code, StringComparison.Ordinal);
            return cmp != 0 ? cmp : a.CompareTo(b);
        }));

        foreach (var node in nodes)
        {
            if (indegree[node.Id] == 0)
                ready.Add(node.Id);
        }

        var order = new List<Guid>(nodes.Count);
        while (ready.Count > 0)
        {
            var id = ready.Min;
            ready.Remove(id);
            order.Add(id);

            foreach (var next in successors[id])
            {
                indegree[next]--;
                if (indegree[next] == 0)
                    ready.Add(next);
            }
        }

        if (order.Count < nodes.Count)
        {
            // Cycle fallback: append leftovers sequentially by SortIndex.
            var seen = new HashSet<Guid>(order);
            foreach (var node in nodes.OrderBy(n => n.SortIndex).ThenBy(n => n.Code, StringComparer.Ordinal))
            {
                if (seen.Add(node.Id))
                    order.Add(node.Id);
            }
        }

        return order;
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
