using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.ProductionOrders;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Schedule;

internal sealed class GetDispatchBoardRequestHandler(
    IProductionOrdersRepository ordersRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IShiftsRepository shiftsRepository,
    IOperatorShiftAssignmentsRepository rosterRepository)
    : IRequestHandler<GetDispatchBoardRequest, DispatchBoardResponse>
{
    internal const int MaxWindowDays = 31;
    internal const int MaxOrderRows = 200;

    public async Task<DispatchBoardResponse> Handle(GetDispatchBoardRequest request, CancellationToken cancellationToken)
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

        // Active shifts for the day buckets. The DB predicate narrows the read;
        // the in-memory re-filter keeps mocked repositories (which ignore the
        // predicate) honest in unit tests.
        var shiftPredicate = PredicateBuilder.New<Shift>(true)
            .And(x => x.IsActive);
        var shifts = (await shiftsRepository.BrowseAsync(
                new Paginator<Shift>(shiftPredicate, UnboundedPaging.Instance), cancellationToken))
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();

        // Roster rows inside the window, aggregated to headcounts per
        // (date, shift) below.
        var rosterPredicate = PredicateBuilder.New<OperatorShiftAssignment>(true)
            .And(x => x.Date >= request.From)
            .And(x => x.Date <= request.To);
        var roster = (await rosterRepository.BrowseAsync(
                new Paginator<OperatorShiftAssignment>(rosterPredicate, UnboundedPaging.Instance), cancellationToken))
            .Where(x => x.Date >= request.From && x.Date <= request.To)
            .ToList();

        var headcounts = roster
            .GroupBy(x => (x.Date, x.ShiftId))
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.OperatorId).Distinct().Count());

        var days = new List<DispatchDayResponse>(dayCount);
        for (var date = request.From; date <= request.To; date = date.AddDays(1))
        {
            var dayShifts = shifts
                .Select(s => new DispatchShiftResponse(
                    s.Id,
                    s.Code,
                    s.Name,
                    s.StartTime,
                    s.EndTime,
                    s.EndTime <= s.StartTime,
                    headcounts.TryGetValue((date, s.Id), out var headcount) ? headcount : 0))
                .ToList();

            days.Add(new DispatchDayResponse(date, dayShifts));
        }

        // Released/InProgress orders only. The DB predicate narrows the read;
        // the in-memory filter below owns the due-date window and ordering so
        // the contract holds regardless of repository behaviour.
        var orderPredicate = PredicateBuilder.New<ProductionOrder>(true)
            .And(x => x.Status == ProductionOrderStatus.Released || x.Status == ProductionOrderStatus.InProgress);
        var candidates = (await ordersRepository.BrowseAsync(
                new Paginator<ProductionOrder>(orderPredicate, UnboundedPaging.Instance), cancellationToken))
            .Where(x => x.Status == ProductionOrderStatus.Released || x.Status == ProductionOrderStatus.InProgress)
            .ToList();

        var rows = candidates
            .Select(o => (Order: o, DueDay: o.DueDate.HasValue ? DateOnly.FromDateTime(o.DueDate.Value) : (DateOnly?)null))
            .Where(x => !x.DueDay.HasValue || x.DueDay.Value < request.From || (x.DueDay.Value >= request.From && x.DueDay.Value <= request.To))
            .Select(x => (x.Order, IsOverdue: x.DueDay.HasValue && x.DueDay.Value < request.From))
            .OrderByDescending(x => x.IsOverdue)
            .ThenBy(x => x.Order.DueDate.HasValue ? 0 : 1)
            .ThenBy(x => x.Order.DueDate)
            .ThenBy(x => x.Order.Priority)
            .ThenBy(x => x.Order.Code, StringComparer.Ordinal)
            .Take(MaxOrderRows)
            .ToList();

        var totals = await confirmationsRepository.GetTotalsForOrdersAsync(
            rows.Select(x => x.Order.Id).ToList(), cancellationToken);

        var orders = rows
            .Select(x =>
            {
                totals.TryGetValue(x.Order.Id, out var t);
                var mapped = ProductionOrderMappers.Map(x.Order, t.ProducedQuantity, t.ScrappedQuantity, t.ConfirmationsCount);
                return new DispatchOrderRowResponse(
                    mapped.Id,
                    mapped.Code,
                    mapped.ProductId,
                    mapped.PlannedQuantity,
                    mapped.ProducedQuantity,
                    mapped.ScrappedQuantity,
                    mapped.RemainingQuantity,
                    mapped.Priority,
                    mapped.DueDate,
                    mapped.Status,
                    x.IsOverdue);
            })
            .ToList();

        return new DispatchBoardResponse(request.From, request.To, days, orders);
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
}
