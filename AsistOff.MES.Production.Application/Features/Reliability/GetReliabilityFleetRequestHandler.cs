using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Reliability;

internal sealed class GetReliabilityFleetRequestHandler(
    IMachinesRepository machinesRepository,
    IDepartmentsRepository departmentsRepository,
    IDowntimeEventsRepository downtimeEventsRepository,
    IMaintenanceWorkOrdersRepository maintenanceWorkOrdersRepository)
    : IRequestHandler<GetReliabilityFleetRequest, IReadOnlyList<ReliabilityFleetRowResponse>>
{
    private const double MaxWindowDays = 93;

    public async Task<IReadOnlyList<ReliabilityFleetRowResponse>> Handle(
        GetReliabilityFleetRequest request, CancellationToken cancellationToken)
    {
        if (request.FromUtc == default || request.ToUtc == default)
            throw new ValidationException(nameof(request.FromUtc), "Time window is required.");

        var fromUtc = request.FromUtc.ToUniversalTime();
        var toUtc = request.ToUtc.ToUniversalTime();

        if (fromUtc >= toUtc)
            throw new ValidationException(nameof(request.FromUtc), "FromUtc must be before ToUtc.");

        if ((toUtc - fromUtc).TotalDays > MaxWindowDays)
            throw new ValidationException(nameof(request.ToUtc), "Time window cannot exceed 93 days.");

        if (request.DepartmentId.HasValue && request.DepartmentId.Value == Guid.Empty)
            throw new ValidationException(nameof(request.DepartmentId), "Department is invalid.");

        // Unknown and cross-tenant departments both yield 404 through the
        // tenant global query filter.
        if (request.DepartmentId.HasValue)
        {
            _ = await departmentsRepository.GetByIdAsync(request.DepartmentId.Value, cancellationToken)
                ?? throw new NotFoundException("Department", request.DepartmentId.Value);
        }

        // Tenant-scoped reads: the global query filter keeps every repository
        // call below inside the caller tenant, so cross-tenant machines never
        // surface. The DB predicate narrows the read; the in-memory re-filter
        // keeps mocked repositories (which ignore the predicate) honest.
        var machinePredicate = PredicateBuilder.New<Machine>(true)
            .And(x => x.IsActive);
        if (request.DepartmentId.HasValue)
            machinePredicate = machinePredicate.And(x => x.DepartmentId == request.DepartmentId.Value);

        var machines = (await machinesRepository.BrowseAsync(
                new Paginator<Machine>(machinePredicate, UnboundedPaging.Instance), cancellationToken))
            .Where(x => x.IsActive)
            .Where(x => !request.DepartmentId.HasValue || x.DepartmentId == request.DepartmentId.Value)
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();

        var windowMinutes = (toUtc - fromUtc).TotalMinutes;
        var rows = new List<ReliabilityFleetRowResponse>(machines.Count);

        foreach (var machine in machines)
        {
            var downtimes = await downtimeEventsRepository.ListOverlappingAsync(
                machine.Id, fromUtc, toUtc, cancellationToken);

            // Only closed-event overlap counts; open events are ignored —
            // exactly like the snapshot.
            var closedOverlaps = downtimes
                .Where(e => e.EndedAt.HasValue)
                .Select(e => OverlapMinutes(e.StartedAt, e.EndedAt!.Value, fromUtc, toUtc))
                .ToList();

            var failureCount = closedOverlaps.Count;
            var totalDowntimeMinutes = Round2(closedOverlaps.Sum());
            var uptimeMinutes = windowMinutes - totalDowntimeMinutes;

            double? mtbfMinutes = failureCount > 0
                ? Round2(uptimeMinutes / failureCount)
                : null;
            double? mttrMinutes = failureCount > 0
                ? Round2(totalDowntimeMinutes / failureCount)
                : null;

            var repairs = await maintenanceWorkOrdersRepository.ListDoneInWindowAsync(
                machine.Id, fromUtc, toUtc, cancellationToken);

            // The repository already filters Done + CompletedAt in window;
            // ignore Open/InProgress/Cancelled defensively so non-Done rows
            // never count — exactly like the snapshot.
            var doneRepairs = repairs
                .Where(r => r.Status == MaintenanceWorkOrderStatus.Done && r.CompletedAt.HasValue)
                .ToList();

            var repairCount = doneRepairs.Count;
            double? avgRepairMinutes = repairCount > 0
                ? Round2(doneRepairs.Average(r =>
                    (r.CompletedAt!.Value - (r.StartedAt ?? r.ReportedAt)).TotalMinutes))
                : null;

            rows.Add(new ReliabilityFleetRowResponse(
                machine.Id,
                machine.Code,
                machine.Name,
                machine.DepartmentId,
                failureCount,
                repairCount,
                windowMinutes,
                uptimeMinutes,
                totalDowntimeMinutes,
                mtbfMinutes,
                mttrMinutes,
                avgRepairMinutes));
        }

        // Worst MTBF first; machines with no measured MTBF (zero failures)
        // sort after every measured machine. Machine code breaks ties so the
        // order is deterministic.
        return rows
            .OrderBy(x => x.MtbfMinutes.HasValue ? 0 : 1)
            .ThenBy(x => x.MtbfMinutes)
            .ThenBy(x => x.MachineCode, StringComparer.Ordinal)
            .ToList();
    }

    internal static double OverlapMinutes(DateTime start, DateTime end, DateTime from, DateTime to)
    {
        var overlapStart = start > from ? start : from;
        var overlapEnd = end < to ? end : to;

        return overlapEnd > overlapStart ? (overlapEnd - overlapStart).TotalMinutes : 0;
    }

    internal static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

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
