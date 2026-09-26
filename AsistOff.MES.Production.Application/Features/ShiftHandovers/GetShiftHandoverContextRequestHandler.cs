using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers;

internal sealed class GetShiftHandoverContextRequestHandler(
    IMachinesRepository machinesRepository,
    IWorkCenterCalendarsRepository calendarsRepository,
    IProductionOrdersRepository ordersRepository,
    IAndonSignalsRepository andonSignalsRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IProductsRepository productsRepository,
    IReasonCodesRepository reasonCodesRepository,
    IOperatorsRepository operatorsRepository)
    : IRequestHandler<GetShiftHandoverContextRequest, ShiftHandoverContextResponse>
{
    internal const int MaxOpenOrders = 100;
    internal const int MaxActiveSignals = 100;

    public async Task<ShiftHandoverContextResponse> Handle(
        GetShiftHandoverContextRequest request, CancellationToken cancellationToken)
    {
        if (request.From == default || request.To == default)
            throw new ValidationException(nameof(request.From), "Time window is required.");

        var fromUtc = request.From.ToUniversalTime();
        var toUtc = request.To.ToUniversalTime();

        if (fromUtc >= toUtc)
            throw new ValidationException(nameof(request.From), "From must be before To.");

        if ((toUtc - fromUtc).TotalHours > GetShiftHandoverContextValidator.MaxWindowHours)
            throw new ValidationException(nameof(request.To),
                $"Time window cannot exceed {GetShiftHandoverContextValidator.MaxWindowHours} hours.");

        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is invalid.");

        // Tenant-scoped reads: the global query filter keeps every repository
        // call below inside the caller tenant, so unknown and cross-tenant ids
        // simply never surface.

        // The global tenant query filter scopes the machine lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        if (request.MachineId.HasValue)
            _ = await machinesRepository.GetByIdAsync(request.MachineId.Value, cancellationToken)
                ?? throw new NotFoundException("Machine", request.MachineId.Value);

        // Shift window from the Work Center calendar entry covering the window
        // start. No entry (or no machine scope) means shiftId null; only a
        // machine-scoped gap is flagged uncovered.
        Guid? shiftId = null;
        var uncoveredShift = false;

        if (request.MachineId.HasValue)
        {
            var calendar = await calendarsRepository.GetByMachineIdAsync(
                request.MachineId.Value, cancellationToken);
            var entry = calendar is null
                ? null
                : ShiftWindowResolver.FindCoveringEntry(calendar.Entries, fromUtc);
            shiftId = entry?.ShiftId;
            uncoveredShift = entry is null;
        }

        var openOrders = await LoadOpenOrdersAsync(cancellationToken);
        var activeSignals = await LoadActiveSignalsAsync(request.MachineId, cancellationToken);
        var (confirmations, totalCount, page, pageSize) = await LoadConfirmationsAsync(
            request, fromUtc, toUtc, cancellationToken);

        return new ShiftHandoverContextResponse(
            request.MachineId,
            shiftId,
            fromUtc,
            toUtc,
            uncoveredShift,
            openOrders,
            activeSignals,
            confirmations,
            totalCount,
            page,
            pageSize);
    }

    private async Task<IReadOnlyList<ShiftHandoverOrderItem>> LoadOpenOrdersAsync(
        CancellationToken cancellationToken)
    {
        // ProductionOrder carries no MachineId FK, so the orders section is
        // tenant-wide open orders; signals and confirmations below carry the
        // machine scope instead.
        var predicate = PredicateBuilder.New<ProductionOrder>(true)
            .And(x => x.Status == ProductionOrderStatus.Released
                || x.Status == ProductionOrderStatus.InProgress);

        var page = await ordersRepository.BrowseAsync(
            new Paginator<ProductionOrder>(
                predicate,
                new FixedPaging(1, MaxOpenOrders, ["Priority", "DueDate", "Code"])),
            cancellationToken);

        // The DB predicate narrows the read; the in-memory re-filter keeps
        // mocked repositories (which ignore the predicate) honest in unit tests.
        var orders = page
            .Where(x => x.Status is ProductionOrderStatus.Released or ProductionOrderStatus.InProgress)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.DueDate is null)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .Take(MaxOpenOrders)
            .ToList();

        var ids = orders.Select(x => x.Id).ToList();
        var totals = await confirmationsRepository.GetTotalsForOrdersAsync(ids, cancellationToken);
        var productCodes = await LoadProductCodesAsync(
            orders.Select(x => x.ProductId).Distinct().ToList(), cancellationToken);

        return orders
            .Select(order =>
            {
                totals.TryGetValue(order.Id, out var t);
                productCodes.TryGetValue(order.ProductId, out var productCode);
                return new ShiftHandoverOrderItem(
                    order.Id,
                    order.Code,
                    order.ProductId,
                    productCode,
                    order.PlannedQuantity,
                    t.ProducedQuantity,
                    t.ScrappedQuantity,
                    order.Priority,
                    order.DueDate,
                    order.Status);
            })
            .ToList();
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
            new Paginator<Product>(predicate, FixedPaging.Unbounded()),
            cancellationToken);

        return products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.Code);
    }

    private async Task<IReadOnlyList<ShiftHandoverSignalItem>> LoadActiveSignalsAsync(
        Guid? machineId, CancellationToken cancellationToken)
    {
        // Only Active signals surface; Acknowledged and Resolved ones are
        // excluded so the incoming crew sees current abnormal conditions.
        var predicate = PredicateBuilder.New<AndonSignal>(true)
            .And(x => x.Status == AndonSignalStatus.Active);

        if (machineId.HasValue)
            predicate = predicate.And(x => x.MachineId == machineId.Value);

        var page = await andonSignalsRepository.BrowseAsync(
            new Paginator<AndonSignal>(
                predicate,
                new FixedPaging(1, MaxActiveSignals, ["RaisedAt,desc"])),
            cancellationToken);

        var signals = page
            .Where(x => x.Status == AndonSignalStatus.Active
                && (!machineId.HasValue || x.MachineId == machineId.Value))
            .OrderByDescending(x => x.RaisedAt)
            .ThenBy(x => x.Id)
            .Take(MaxActiveSignals)
            .ToList();

        var reasonIds = signals
            .Where(x => x.ReasonCodeId.HasValue)
            .Select(x => x.ReasonCodeId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string> reasonCodes = new();
        if (reasonIds.Count > 0)
        {
            var reasons = await reasonCodesRepository.ListByIdsAsync(reasonIds, cancellationToken);
            reasonCodes = reasons.ToDictionary(x => x.Id, x => x.Code);
        }

        return signals
            .Select(signal =>
            {
                var code = signal.ReasonCodeId.HasValue
                    && reasonCodes.TryGetValue(signal.ReasonCodeId.Value, out var reasonCode)
                    ? reasonCode
                    : signal.Category.ToString();
                return new ShiftHandoverSignalItem(
                    signal.Id,
                    signal.MachineId,
                    signal.Category,
                    signal.Category.ToString(),
                    code,
                    signal.RaisedAt);
            })
            .ToList();
    }

    private async Task<(
        IReadOnlyList<ShiftHandoverConfirmationItem> Items,
        int TotalCount,
        int Page,
        int PageSize)> LoadConfirmationsAsync(
        GetShiftHandoverContextRequest request,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<ProductionConfirmation>(true)
            .And(x => x.ReportedAt >= fromUtc)
            .And(x => x.ReportedAt <= toUtc);

        if (request.MachineId.HasValue)
            predicate = predicate.And(x => x.MachineId == request.MachineId.Value);

        var totalCount = await confirmationsRepository.CountAsync(predicate, cancellationToken);

        var page = request.ConfirmationPage is > 0 ? request.ConfirmationPage.Value : 1;
        var pageSize = request.ConfirmationPageSize is > 0
            ? Math.Min(request.ConfirmationPageSize.Value, GetShiftHandoverContextValidator.MaxConfirmationPageSize)
            : GetShiftHandoverContextValidator.DefaultConfirmationPageSize;

        var rows = await confirmationsRepository.BrowseAsync(
            new Paginator<ProductionConfirmation>(
                predicate,
                new FixedPaging(page, pageSize, ["ReportedAt,desc"])),
            cancellationToken);

        // Newest first; the in-memory re-filter/order guards mocked
        // repositories while the database applies it first in production.
        var items = rows
            .Where(x => x.ReportedAt >= fromUtc
                && x.ReportedAt <= toUtc
                && (!request.MachineId.HasValue || x.MachineId == request.MachineId.Value))
            .OrderByDescending(x => x.ReportedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var operatorCodes = await LoadOperatorCodesAsync(
            items.Where(x => x.ReportedByOperatorId.HasValue)
                .Select(x => x.ReportedByOperatorId!.Value)
                .Distinct()
                .ToList(),
            cancellationToken);

        var mapped = items
            .Select(confirmation =>
            {
                operatorCodes.TryGetValue(
                    confirmation.ReportedByOperatorId ?? Guid.Empty, out var operatorCode);
                return new ShiftHandoverConfirmationItem(
                    confirmation.Id,
                    confirmation.ProductionOrderId,
                    confirmation.MachineId,
                    confirmation.ReportedAt,
                    confirmation.GoodQuantity,
                    confirmation.ScrapQuantity,
                    operatorCode,
                    confirmation.Notes);
            })
            .ToList();

        return (mapped, totalCount, page, pageSize);
    }

    private async Task<Dictionary<Guid, string>> LoadOperatorCodesAsync(
        List<Guid> operatorIds, CancellationToken cancellationToken)
    {
        if (operatorIds.Count == 0)
            return new Dictionary<Guid, string>();

        var predicate = PredicateBuilder.New<Operator>(false);
        foreach (var id in operatorIds)
            predicate = predicate.Or(x => x.Id == id);

        var operators = await operatorsRepository.BrowseAsync(
            new Paginator<Operator>(predicate, FixedPaging.Unbounded()),
            cancellationToken);

        return operators
            .Where(x => operatorIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.Identifier);
    }

    /// <summary>
    /// Fixed paging owned by the handler: bounded first page for the capped
    /// sections, explicit page/size/sort for the paged confirmations, or
    /// unbounded (null page/size, empty sort) for the read-time enrichment
    /// lookups where the handler filters in memory.
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
