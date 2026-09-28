using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Providers;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Schedule;

internal sealed class GetOperatorShiftQueueRequestHandler(
    IOperatorsRepository operatorsRepository,
    IOperatorShiftAssignmentsRepository assignmentsRepository,
    IShiftsRepository shiftsRepository,
    IProductionOrdersRepository ordersRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IScheduledOperationsRepository scheduledOperationsRepository,
    IMachinesRepository machinesRepository,
    IProductsRepository productsRepository,
    IAndonSignalsRepository andonSignalsRepository,
    IDateTimeProvider dateTimeProvider,
    IOperationNodesRepository operationNodesRepository,
    ISkillsRepository skillsRepository,
    IOperatorSkillQualificationsRepository qualificationsRepository)
    : IRequestHandler<GetOperatorShiftQueueRequest, OperatorShiftQueueResponse>
{
    internal const int MaxQueueRows = 200;
    internal const int MaxActiveSignals = 100;

    public async Task<OperatorShiftQueueResponse> Handle(
        GetOperatorShiftQueueRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OperatorCode))
            throw new ValidationException(nameof(request.OperatorCode), "Operator code is required.");

        // Oversized Take is clamped to the dispatch board bound; Take <= 0 is
        // rejected by the validator, with this guard keeping mocked-validator
        // unit tests honest.
        if (request.Take is <= 0)
            throw new ValidationException(nameof(request.Take), "Take must be at least 1.");

        var take = request.Take is null or > MaxQueueRows ? MaxQueueRows : request.Take.Value;

        // Tenant-scoped reads: the global query filter keeps every repository
        // call below inside the caller tenant, so unknown and cross-tenant
        // codes simply never surface.

        // Operator by code (Identifier). The DB predicate narrows the read;
        // the in-memory re-filter keeps mocked repositories (which ignore the
        // predicate) honest in unit tests.
        var operatorPredicate = PredicateBuilder.New<Operator>(true)
            .And(x => x.Identifier == request.OperatorCode);
        var match = (await operatorsRepository.BrowseAsync(
                new Paginator<Operator>(operatorPredicate, UnboundedPaging.Instance), cancellationToken))
            .FirstOrDefault(x => x.Identifier == request.OperatorCode)
            ?? throw new NotFoundException("Operator", request.OperatorCode);

        var assignmentPredicate = PredicateBuilder.New<OperatorShiftAssignment>(true)
            .And(x => x.OperatorId == match.Id);
        var assignments = (await assignmentsRepository.BrowseAsync(
                new Paginator<OperatorShiftAssignment>(assignmentPredicate, UnboundedPaging.Instance), cancellationToken))
            .Where(x => x.OperatorId == match.Id)
            .ToList();

        // No roster row at all: empty queue with operator context, not 404.
        if (assignments.Count == 0)
            return Empty(request.OperatorCode, match.Id);

        var shiftIds = assignments.Select(x => x.ShiftId).Distinct().ToList();
        var shiftsById = (await shiftsRepository.GetByIdsAsync(shiftIds, cancellationToken))
            .ToDictionary(x => x.Id);

        var nowUtc = dateTimeProvider.UtcNow;

        // The covering assignment is the one whose [start, end) window holds
        // now. Overnight shifts end on the day after the roster date.
        var covering = assignments
            .Where(x => shiftsById.ContainsKey(x.ShiftId))
            .Select(x => (Assignment: x, Shift: shiftsById[x.ShiftId]))
            .Select(x => (x.Assignment, x.Shift, Window: ShiftWindow(x.Assignment.Date, x.Shift)))
            .Where(x => x.Window.Start <= nowUtc && nowUtc < x.Window.End)
            .OrderBy(x => x.Window.Start)
            .ThenBy(x => x.Shift.Id)
            .FirstOrDefault();

        // Rostered but off shift right now: empty queue with operator context.
        if (covering.Assignment is null)
            return Empty(request.OperatorCode, match.Id);

        var shift = covering.Shift;
        var isOvernight = shift.EndTime <= shift.StartTime;
        var shiftContext = new OperatorShiftContextResponse(
            shift.Id,
            shift.Code,
            shift.Name,
            covering.Assignment.Date,
            covering.Window.Start,
            covering.Window.End,
            isOvernight);

        // Same bounded server-side read as the dispatch board (issue #274):
        // Released/InProgress orders overlapping the shift window, capped at
        // Take rows. The date window spans the roster date plus the next day
        // for overnight shifts.
        var fromDate = covering.Assignment.Date;
        var toDate = isOvernight ? fromDate.AddDays(1) : fromDate;
        var candidates = await ordersRepository.BrowseDispatchBoardAsync(
            fromDate, toDate, take, cancellationToken);

        // Next-up ordering (priority, then due date with nulls last) differs
        // from the board's overdue-first ordering, so it is applied in memory
        // over the bounded candidate set. The defensive Take below only guards
        // mocked repositories; the database applies it first.
        var queued = candidates
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.DueDate is null)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .Take(take)
            .ToList();

        var orderIds = queued.Select(x => x.Id).ToList();
        var totals = await confirmationsRepository.GetTotalsForOrdersAsync(orderIds, cancellationToken);

        // ProductionOrder carries no Work Center FK: the scheduled lane comes
        // from the manual ScheduledOperation override when one exists,
        // otherwise the order is unscheduled (null, as on the Gantt
        // unassigned lane).
        var machineByOrder = new Dictionary<Guid, Guid>();
        if (orderIds.Count > 0)
        {
            var overrides = await scheduledOperationsRepository.ListForOrdersAsync(orderIds, cancellationToken);
            machineByOrder = overrides
                .Where(x => orderIds.Contains(x.ProductionOrderId))
                .GroupBy(x => x.ProductionOrderId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(o => o.PlannedStart).First().MachineId);
        }

        var machinesById = (await machinesRepository.BrowseAsync(
                new Paginator<Machine>(PredicateBuilder.New<Machine>(true), UnboundedPaging.Instance), cancellationToken))
            .ToDictionary(x => x.Id);

        var productCodes = await LoadProductCodesAsync(
            queued.Select(x => x.ProductId).Distinct().ToList(), cancellationToken);

        // Skill-gap flags (issue #397): the shift crew is everyone rostered
        // to the covering shift on its date; an order is flagged when its
        // recipe operations require at least one skill and zero crew members
        // hold every required skill.
        var requiredByVersion = await OperatorSkillGating.LoadRequiredSkillsByVersionAsync(
            operationNodesRepository, skillsRepository,
            queued.Select(x => x.RecipeVersionId).Distinct().ToList(), cancellationToken);
        var crewPredicate = PredicateBuilder.New<OperatorShiftAssignment>(true)
            .And(x => x.Date == covering.Assignment.Date)
            .And(x => x.ShiftId == covering.Shift.Id);
        var crewIds = (await assignmentsRepository.BrowseAsync(
                new Paginator<OperatorShiftAssignment>(crewPredicate, UnboundedPaging.Instance), cancellationToken))
            .Where(x => x.Date == covering.Assignment.Date && x.ShiftId == covering.Shift.Id)
            .Select(x => x.OperatorId)
            .Distinct()
            .ToList();
        var heldByOperator = await qualificationsRepository.ListSkillCodesForOperatorsAsync(
            crewIds, cancellationToken);
        var heldSets = crewIds
            .Select(id => heldByOperator.TryGetValue(id, out var codes) ? codes : [])
            .ToList();

        var orders = queued
            .Select(order =>
            {
                totals.TryGetValue(order.Id, out var t);
                var remaining = order.PlannedQuantity - t.ProducedQuantity;
                if (remaining < 0)
                    remaining = 0;

                machineByOrder.TryGetValue(order.Id, out var machineIdValue);
                Guid? machineId = machineByOrder.ContainsKey(order.Id) ? machineIdValue : null;
                machinesById.TryGetValue(machineId ?? Guid.Empty, out var machine);
                productCodes.TryGetValue(order.ProductId, out var productCode);
                requiredByVersion.TryGetValue(order.RecipeVersionId, out var required);
                required ??= [];

                return new OperatorShiftQueuedOrderResponse(
                    order.Id,
                    order.Code,
                    order.ProductId,
                    productCode,
                    order.PlannedQuantity,
                    t.ProducedQuantity,
                    t.ScrappedQuantity,
                    remaining,
                    machineId,
                    machine?.Code,
                    machine?.Name,
                    order.Priority,
                    order.DueDate,
                    order.Status,
                    !OperatorSkillGating.HasQualifiedOperator(required, heldSets));
            })
            .ToList();

        var signals = await LoadActiveSignalsAsync(
            machineByOrder.Values.Distinct().ToList(), machinesById, cancellationToken);

        return new OperatorShiftQueueResponse(
            request.OperatorCode, match.Id, shiftContext, orders, signals);
    }

    private static OperatorShiftQueueResponse Empty(string operatorCode, Guid operatorId)
        => new(operatorCode, operatorId, null, [], []);

    private static (DateTime Start, DateTime End) ShiftWindow(DateOnly date, Shift shift)
    {
        var start = date.ToDateTime(shift.StartTime, DateTimeKind.Utc);
        var endDate = shift.EndTime <= shift.StartTime ? date.AddDays(1) : date;
        var end = endDate.ToDateTime(shift.EndTime, DateTimeKind.Utc);
        return (start, end);
    }

    private async Task<Dictionary<Guid, string>> LoadProductCodesAsync(
        List<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
            return new Dictionary<Guid, string>();

        var predicate = PredicateBuilder.New<Product>(false);
        foreach (var id in productIds)
            predicate = predicate.Or(x => x.Id == id);

        var products = await productsRepository.BrowseAsync(
            new Paginator<Product>(predicate, FixedPaging.Unbounded()), cancellationToken);

        return products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.Code);
    }

    private async Task<IReadOnlyList<OperatorShiftQueueSignalResponse>> LoadActiveSignalsAsync(
        List<Guid> machineIds,
        Dictionary<Guid, Machine> machinesById,
        CancellationToken cancellationToken)
    {
        // Signals are scoped to the queued Work Centers. With no scheduled
        // lanes there is nothing to scope to, so the signal list is empty.
        if (machineIds.Count == 0)
            return [];

        // One OR-branch per queued Work Center, each already intersected
        // with Active so the database applies the same scoping the
        // in-memory re-filter below enforces for mocked repositories.
        var predicate = PredicateBuilder.New<AndonSignal>(false);
        foreach (var id in machineIds)
        {
            var captured = id;
            predicate = predicate.Or(x => x.Status == AndonSignalStatus.Active && x.MachineId == captured);
        }
        var page = await andonSignalsRepository.BrowseAsync(
            new Paginator<AndonSignal>(
                predicate,
                new FixedPaging(1, MaxActiveSignals, ["RaisedAt,desc"])),
            cancellationToken);

        return page
            .Where(x => x.Status == AndonSignalStatus.Active && machineIds.Contains(x.MachineId))
            .OrderByDescending(x => x.RaisedAt)
            .ThenBy(x => x.Id)
            .Take(MaxActiveSignals)
            .Select(signal =>
            {
                machinesById.TryGetValue(signal.MachineId, out var machine);
                return new OperatorShiftQueueSignalResponse(
                    signal.Id,
                    signal.MachineId,
                    machine?.Code,
                    signal.Category,
                    signal.Category.ToString(),
                    signal.RaisedAt);
            })
            .ToList();
    }

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

    /// <summary>
    /// Fixed paging owned by the handler: bounded first page for the capped
    /// sections, or unbounded (null page/size, empty sort) for the read-time
    /// enrichment lookups where the handler filters in memory.
    /// </summary>
    private sealed class FixedPaging(
        int? pageNumber, int? pageSize, List<string> sort) : IPagedRequest
    {
        public List<string> RawSort { get; set; } = sort;
        public IReadOnlyCollection<string> SupportedSortFields { get; } = [];
        public int? PageNumber => pageNumber;
        public int? PageSize => pageSize;
        public int? MaxPageSize => null;

        public static FixedPaging Unbounded() => new(null, null, []);
    }
}
