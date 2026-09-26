using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Schedule;

internal sealed class GetGanttScheduleRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IOperationNodesRepository operationsRepository,
    IScheduledOperationsRepository scheduledOperationsRepository,
    IMachinesRepository machinesRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetGanttScheduleRequest, GanttScheduleResponse>
{
    internal const int MaxWindowDays = 31;
    internal const int MaxOrderRows = 200;

    public async Task<GanttScheduleResponse> Handle(GetGanttScheduleRequest request, CancellationToken cancellationToken)
    {
        if (request.From == default || request.To == default)
            throw new ValidationException(nameof(request.From), "Date window is required.");

        if (request.From > request.To)
            throw new ValidationException(nameof(request.From), "From must not be after To.");

        var dayCount = request.To.DayNumber - request.From.DayNumber + 1;
        if (dayCount > MaxWindowDays)
            throw new ValidationException(nameof(request.To), $"Time window cannot exceed {MaxWindowDays} days.");

        // Tenant-scoped reads: the global query filter keeps every repository
        // call below inside the caller tenant, so unknown and cross-tenant ids
        // simply never surface.

        // Same bounded server-side order set as the dispatch board (issue
        // #274): Released/InProgress orders that are overdue, due inside the
        // window or have no due date, capped at 200 rows.
        var candidates = await ordersRepository.BrowseDispatchBoardAsync(
            request.From, request.To, MaxOrderRows, cancellationToken);

        var orders = candidates.Take(MaxOrderRows).ToList();
        if (orders.Count == 0)
            return new GanttScheduleResponse(request.From, request.To, []);

        var versionIds = orders.Select(o => o.RecipeVersionId).Distinct().ToList();
        var nodes = await operationsRepository.ListForVersionsAsync(versionIds, cancellationToken);
        var nodesByVersion = nodes.GroupBy(n => n.RecipeVersionId).ToDictionary(g => g.Key, g => (IReadOnlyCollection<OperationNode>)g.ToList());

        var orderIds = orders.Select(o => o.Id).ToList();
        var overrides = await scheduledOperationsRepository.ListForOrdersAsync(orderIds, cancellationToken);
        var overridesByKey = overrides
            .GroupBy(o => (o.ProductionOrderId, o.OperationNodeId))
            .ToDictionary(g => g.Key, g => g.First());

        var machines = await machinesRepository.BrowseAsync(
            new Paginator<Machine>(LinqKit.PredicateBuilder.New<Machine>(true), UnboundedPaging.Instance), cancellationToken);
        var machinesById = machines.ToDictionary(m => m.Id);

        var windowStartUtc = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var windowEndExclusiveUtc = request.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var anchorNowUtc = dateTimeProvider.UtcNow;

        // Guid.Empty is the sentinel key for the unassigned lane (a nullable
        // Guid cannot satisfy the Dictionary notnull constraint, and CS8714
        // would add a new build warning).
        var barsByMachine = new Dictionary<Guid, List<GanttBarResponse>>();
        foreach (var order in orders)
        {
            if (!nodesByVersion.TryGetValue(order.RecipeVersionId, out var versionNodes))
                continue;

            var segments = GanttScheduler.ComputeOrderSegments(order, versionNodes, anchorNowUtc).ToList();

            // Fully-elapsed chains (an overdue order anchored backward from a
            // due date before the window) are pulled forward to start at the
            // window start so late work stays visible instead of vanishing
            // before the window. Durations are preserved; the bars then end
            // after the due date and read overdue.
            if (segments.Count > 0 && segments.Max(s => s.PlannedEnd) < windowStartUtc)
            {
                var shift = windowStartUtc - segments.Min(s => s.PlannedStart);
                segments = segments
                    .Select(s => s with { PlannedStart = s.PlannedStart + shift, PlannedEnd = s.PlannedEnd + shift })
                    .ToList();
            }

            foreach (var segment in segments)
            {
                var plannedStart = segment.PlannedStart;
                var plannedEnd = segment.PlannedEnd;
                Guid? machineId = segment.MachineId;

                // Manual overrides survive recomputation: a persisted row for
                // this (order, operation) pins the bar's window and lane.
                if (overridesByKey.TryGetValue((order.Id, segment.OperationNodeId), out var manual))
                {
                    plannedStart = manual.PlannedStart;
                    plannedEnd = manual.PlannedEnd;
                    machineId = manual.MachineId;
                }

                // Keep only bars overlapping the requested window.
                if (plannedEnd <= windowStartUtc || plannedStart >= windowEndExclusiveUtc)
                    continue;

                if (request.MachineId.HasValue && machineId != request.MachineId.Value)
                    continue;

                var isOverdue = order.DueDate.HasValue && plannedEnd > EnsureUtc(order.DueDate.Value);

                if (!barsByMachine.TryGetValue(machineId ?? Guid.Empty, out var bars))
                {
                    bars = [];
                    barsByMachine[machineId ?? Guid.Empty] = bars;
                }

                bars.Add(new GanttBarResponse(
                    order.Id,
                    order.Code,
                    segment.OperationNodeId,
                    segment.OperationCode,
                    segment.OperationName,
                    machineId,
                    plannedStart,
                    plannedEnd,
                    isOverdue));
            }
        }

        var groups = barsByMachine
            .Select(kvp =>
            {
                var machineKey = kvp.Key == Guid.Empty ? (Guid?)null : kvp.Key;
                machinesById.TryGetValue(kvp.Key, out var machine);
                var bars = kvp.Value
                    .OrderBy(b => b.PlannedStart)
                    .ThenBy(b => b.ProductionOrderCode, StringComparer.Ordinal)
                    .ThenBy(b => b.OperationCode, StringComparer.Ordinal)
                    .ToList();
                return new GanttMachineGroupResponse(
                    machineKey,
                    machine?.Code,
                    machine?.Name,
                    bars);
            })
            .OrderBy(g => g.MachineCode ?? "\uffff", StringComparer.Ordinal)
            .ThenBy(g => g.MachineId)
            .ToList();

        return new GanttScheduleResponse(request.From, request.To, groups);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>
    /// Unbounded paging: null page number/size disables Skip/Take in
    /// <c>PageFilter</c>, and an empty sort leaves the source unordered so the
    /// handler owns filtering and ordering in memory.
    /// </summary>
    private sealed class UnboundedPaging : IPagedRequest
    {
        public static readonly UnboundedPaging Instance = new();

        public List<string> RawSort { get; set; } = [];
        public IReadOnlyCollection<string> SupportedSortFields { get; } = [];
        public int? PageNumber => null;
        public int? PageSize => null;
        public int? MaxPageSize => null;
    }
}
