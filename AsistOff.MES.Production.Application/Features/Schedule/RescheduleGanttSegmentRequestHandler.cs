using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.ProductionOrders;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Schedule;

internal sealed class RescheduleGanttSegmentRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IOperationNodesRepository operationsRepository,
    IScheduledOperationsRepository scheduledOperationsRepository,
    IMachinesRepository machinesRepository,
    IShiftsRepository shiftsRepository,
    IOperatorShiftAssignmentsRepository rosterRepository,
    IGuidProvider guidProvider,
    ITenantContext tenantContext)
    : IRequestHandler<RescheduleGanttSegmentRequest, RescheduleGanttSegmentResponse>
{
    internal const int MaxWindowDays = 31;

    public async Task<RescheduleGanttSegmentResponse> Handle(
        RescheduleGanttSegmentRequest request, CancellationToken cancellationToken)
    {
        // Shape checks mirror RescheduleGanttSegmentValidator so direct handler
        // calls (and unit tests bypassing the pipeline) fail with the same
        // 400s as the ValidationBehavior path.
        if (request.PlannedStart == default || request.PlannedEnd == default)
            throw new ValidationException(nameof(request.PlannedStart), "Planned window is required.");

        var start = EnsureUtc(request.PlannedStart);
        var end = EnsureUtc(request.PlannedEnd);

        if (start >= end)
            throw new ValidationException(nameof(request.PlannedStart), "Planned start must be before planned end.");

        if ((end - start).TotalDays > MaxWindowDays)
            throw new ValidationException(nameof(request.PlannedEnd), $"Moved window cannot exceed {MaxWindowDays} days.");

        // Tenant-scoped reads: the global query filter keeps every repository
        // call below inside the caller tenant, so unknown and cross-tenant ids
        // simply never surface (404) and nothing is persisted for them.
        var order = await ordersRepository.GetAsync(request.ProductionOrderId, cancellationToken)
            ?? throw new NotFoundException("ProductionOrder", request.ProductionOrderId);

        // Required token: missing or malformed is a 400, a well-formed but
        // stale token is a 409 carrying the current token with retry guidance.
        // The move itself does not bump the order xmin, so one token covers
        // consecutive moves until the order itself changes.
        ProductionOrderConcurrency.RequireMatchForUpdate(order, request.ConcurrencyToken);

        var node = await operationsRepository.GetAsync(request.OperationNodeId, cancellationToken)
            ?? throw new NotFoundException("OperationNode", request.OperationNodeId);

        if (node.RecipeVersionId != order.RecipeVersionId)
            throw new NotFoundException("OperationNode", request.OperationNodeId);

        var machine = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        // Capacity leveling against persisted overrides on the target Work
        // Center. Without force an overlap is a 409 listing the conflicting
        // segment ids; with force the move persists anyway and the bypassed
        // ids are echoed in the response.
        var lane = await scheduledOperationsRepository.ListForMachineAsync(request.MachineId, cancellationToken);
        var conflicts = GanttLeveling.FindConflicts(lane, start, end, order.Id, node.Id);

        if (conflicts.Count > 0 && !request.Force)
            throw new GanttScheduleConflictException(conflicts.Select(c => c.Id).ToList());

        var warning = await EvaluateShiftCoverageAsync(start, end, cancellationToken);

        var existing = (await scheduledOperationsRepository.ListForOrdersAsync([order.Id], cancellationToken))
            .FirstOrDefault(o => o.OperationNodeId == node.Id);

        ScheduledOperation saved;
        if (existing is null)
        {
            saved = new ScheduledOperation
            {
                Id = guidProvider.NewGuid(),
                TenantId = tenantContext.TenantId,
                ProductionOrderId = order.Id,
                OperationNodeId = node.Id,
                MachineId = machine.Id,
                PlannedStart = start,
                PlannedEnd = end,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };
            saved = await scheduledOperationsRepository.AddAsync(saved, cancellationToken);
        }
        else
        {
            existing.MachineId = machine.Id;
            existing.PlannedStart = start;
            existing.PlannedEnd = end;
            existing.Notes = request.Notes;
            await scheduledOperationsRepository.UpdateAsync(existing, cancellationToken);
            saved = existing;
        }

        return new RescheduleGanttSegmentResponse(
            saved.Id,
            order.Id,
            node.Id,
            machine.Id,
            start,
            end,
            warning,
            request.Force ? conflicts.Select(c => c.Id).ToList() : []);
    }

    /// <summary>
    /// Advisory shift coverage for the moved window, reusing the dispatch
    /// board's read-only sources: active shifts plus roster headcounts for the
    /// spanned dates. Never blocks the move.
    /// </summary>
    private async Task<bool> EvaluateShiftCoverageAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var fromDate = DateOnly.FromDateTime(start);
        var toDate = DateOnly.FromDateTime(end);

        // The DB predicate narrows the read; the in-memory re-filter keeps
        // mocked repositories (which ignore the predicate) honest in unit tests.
        var shiftPredicate = PredicateBuilder.New<Shift>(true)
            .And(x => x.IsActive);
        var shifts = (await shiftsRepository.BrowseAsync(
                new Paginator<Shift>(shiftPredicate, UnboundedPaging.Instance), cancellationToken))
            .Where(x => x.IsActive)
            .ToList();

        var rosterPredicate = PredicateBuilder.New<OperatorShiftAssignment>(true)
            .And(x => x.Date >= fromDate)
            .And(x => x.Date <= toDate);
        var roster = (await rosterRepository.BrowseAsync(
                new Paginator<OperatorShiftAssignment>(rosterPredicate, UnboundedPaging.Instance), cancellationToken))
            .Where(x => x.Date >= fromDate && x.Date <= toDate)
            .ToList();

        return GanttShiftCoverage.HasCoverageWarning(shifts, roster, fromDate, toDate);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            // A local timestamp denotes a real instant: convert it instead of
            // relabelling the ticks, otherwise the pinned window would land on
            // a shifted moment on non-UTC hosts.
            DateTimeKind.Local => value.ToUniversalTime(),
            // Persisted timestamps (timestamptz) materialize as UTC, so an
            // unspecified kind means "already UTC", not local.
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

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
