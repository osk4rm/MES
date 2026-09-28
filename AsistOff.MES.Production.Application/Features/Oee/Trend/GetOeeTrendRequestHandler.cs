using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Oee.Trend;

internal sealed class GetOeeTrendRequestHandler(
    IMachinesRepository machinesRepository,
    IWorkCenterCalendarsRepository calendarsRepository,
    IDowntimeEventsRepository downtimeEventsRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IProductionOrdersRepository ordersRepository,
    IOperationNodesRepository operationsRepository)
    : IRequestHandler<GetOeeTrendRequest, OeeTrendResponse>
{
    private const double MaxWindowDays = 93;

    public async Task<OeeTrendResponse> Handle(GetOeeTrendRequest request, CancellationToken cancellationToken)
    {
        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");

        if (request.FromUtc == default || request.ToUtc == default)
            throw new ValidationException(nameof(request.FromUtc), "Time window is required.");

        var fromUtc = request.FromUtc.ToUniversalTime();
        var toUtc = request.ToUtc.ToUniversalTime();

        if (fromUtc >= toUtc)
            throw new ValidationException(nameof(request.FromUtc), "FromUtc must be before ToUtc.");

        if ((toUtc - fromUtc).TotalDays > MaxWindowDays)
            throw new ValidationException(nameof(request.ToUtc), "Time window cannot exceed 93 days.");

        if (request.IdealCycleTimeSeconds.HasValue && request.IdealCycleTimeSeconds.Value <= 0)
            throw new ValidationException(nameof(request.IdealCycleTimeSeconds), "Ideal cycle time must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.Bucket)
            || !Enum.TryParse<OeeTrendBucket>(request.Bucket, ignoreCase: true, out var bucket)
            || !Enum.IsDefined(bucket))
            throw new ValidationException(nameof(request.Bucket), "Bucket must be either Day or Week.");

        // The global tenant query filter scopes the machine lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var calendar = await calendarsRepository.GetByMachineIdAsync(request.MachineId, cancellationToken);

        // Single full-window fetch, sliced per bucket in memory: the overlap
        // of an event with a bucket is contained in its overlap with the
        // window, so per-bucket math equals a per-bucket snapshot query.
        var downtimes = await downtimeEventsRepository.ListOverlappingAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);
        var confirmations = await confirmationsRepository.ListForMachineInWindowAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        var closedDowntimes = downtimes.Where(e => e.EndedAt.HasValue).ToList();
        var buckets = BuildBuckets(bucket, fromUtc, toUtc);

        // Ideal cycle time: an explicit positive value wins for the whole
        // trend; otherwise resolve from the confirmed orders' operations for
        // the full window (same minimum-positive rule as the summary
        // endpoint). A null ideal with no resolvable routing data is a 400
        // naming IdealCycleTimeSeconds — never 500 or zero-division.
        decimal windowIdeal;
        string windowSource;
        if (request.IdealCycleTimeSeconds.HasValue)
        {
            windowIdeal = request.IdealCycleTimeSeconds.Value;
            windowSource = OeeIdealCycleTimeResolver.CallerSource;
        }
        else
        {
            var resolved = await OeeIdealCycleTimeResolver.ResolveAsync(
                confirmations, ordersRepository, operationsRepository, cancellationToken);
            if (!resolved.HasValue)
                throw new ValidationException(
                    nameof(request.IdealCycleTimeSeconds),
                    "Ideal cycle time could not be resolved from routing master data for the window; supply idealCycleTimeSeconds.");
            windowIdeal = resolved.Value;
            windowSource = OeeIdealCycleTimeResolver.RoutingSource;
        }

        var entries = new List<OeeSnapshotResponse>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
        {
            var (bucketFrom, bucketTo) = buckets[i];
            var isLast = i == buckets.Count - 1;

            // Per-bucket ideal: an explicit value reuses the window ideal;
            // otherwise resolve from the bucket's own confirmations so each
            // entry matches the snapshot for that sub-window, falling back
            // to the window ideal for empty buckets (performance stays null
            // there anyway because the count is zero).
            decimal bucketIdeal = windowIdeal;
            if (!request.IdealCycleTimeSeconds.HasValue)
            {
                var inBucketForIdeal = FilterConfirmations(confirmations, bucketFrom, bucketTo, isLast);
                if (inBucketForIdeal.Count > 0)
                {
                    var bucketResolved = await OeeIdealCycleTimeResolver.ResolveAsync(
                        inBucketForIdeal, ordersRepository, operationsRepository, cancellationToken);
                    if (bucketResolved.HasValue)
                        bucketIdeal = bucketResolved.Value;
                }
            }

            entries.Add(BuildSnapshot(
                request.MachineId,
                calendar?.Entries,
                closedDowntimes,
                confirmations,
                bucketFrom,
                bucketTo,
                isLast,
                bucketIdeal,
                windowSource));
        }

        return new OeeTrendResponse(
            request.MachineId,
            fromUtc,
            toUtc,
            windowIdeal,
            bucket.ToString(),
            entries,
            windowSource);
    }

    /// <summary>
    /// Partitions <c>[fromUtc, toUtc)</c> into calendar-aligned buckets in
    /// ascending order. Day buckets break at UTC midnights, week buckets at
    /// Monday 00:00 UTC; edge buckets are clipped to the query window.
    /// </summary>
    internal static IReadOnlyList<(DateTime From, DateTime To)> BuildBuckets(
        OeeTrendBucket bucket, DateTime fromUtc, DateTime toUtc)
    {
        var result = new List<(DateTime From, DateTime To)>();

        DateTime cursor = bucket == OeeTrendBucket.Day
            ? fromUtc.Date
            : fromUtc.Date.AddDays(-DaysSinceMonday(fromUtc.Date));
        var stepDays = bucket == OeeTrendBucket.Day ? 1 : 7;

        while (cursor < toUtc)
        {
            var bucketFrom = cursor < fromUtc ? fromUtc : cursor;
            var bucketTo = cursor.AddDays(stepDays) > toUtc ? toUtc : cursor.AddDays(stepDays);

            if (bucketFrom < bucketTo)
                result.Add((bucketFrom, bucketTo));

            cursor = cursor.AddDays(stepDays);
        }

        return result;
    }

    private static int DaysSinceMonday(DateTime date)
        => ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

    /// <summary>
    /// Half-open buckets except the trailing edge: a confirmation stamped
    /// exactly on an interior boundary belongs to the later bucket only,
    /// so the union of buckets equals the inclusive full-window query.
    /// </summary>
    private static IReadOnlyCollection<ProductionConfirmation> FilterConfirmations(
        IReadOnlyCollection<ProductionConfirmation> confirmations,
        DateTime bucketFrom,
        DateTime bucketTo,
        bool isLast)
        => confirmations.Where(c =>
                c.ReportedAt >= bucketFrom && (c.ReportedAt < bucketTo || (isLast && c.ReportedAt <= bucketTo)))
            .ToList();

    /// <summary>
    /// Snapshot-equivalent computation for one bucket: planned time from the
    /// calendar entries overlapped with the bucket, run time as planned time
    /// minus closed downtime overlap (open events ignored, never negative),
    /// counts from confirmations in the bucket. Null rules mirror
    /// <c>GetOeeSnapshotRequestHandler</c> exactly.
    /// </summary>
    private static OeeSnapshotResponse BuildSnapshot(
        Guid machineId,
        IEnumerable<Configuration.Domain.Entities.WorkCenterCalendarEntry>? entries,
        IReadOnlyCollection<DowntimeEvent> closedDowntimes,
        IReadOnlyCollection<ProductionConfirmation> confirmations,
        DateTime bucketFrom,
        DateTime bucketTo,
        bool isLast,
        decimal idealCycleTimeSeconds,
        string idealCycleTimeSource)
    {
        var plannedMinutes = OeeMath.PlannedMinutes(entries, bucketFrom, bucketTo);

        var downtimeMinutes = closedDowntimes
            .Sum(e => OeeMath.OverlapMinutes(e.StartedAt, e.EndedAt!.Value, bucketFrom, bucketTo));

        var runMinutes = Math.Max(0, plannedMinutes - downtimeMinutes);

        var inBucket = FilterConfirmations(confirmations, bucketFrom, bucketTo, isLast);

        var goodCount = inBucket.Sum(c => c.GoodQuantity);
        var scrapCount = inBucket.Sum(c => c.ScrapQuantity);
        var totalCount = goodCount + scrapCount;

        double? availability = null;
        double? performance = null;
        double? quality = null;

        if (plannedMinutes > 0 && runMinutes > 0)
            availability = OeeMath.Round4(runMinutes / plannedMinutes);

        if (plannedMinutes > 0 && runMinutes > 0 && totalCount > 0)
            performance = OeeMath.Round4((double)(totalCount * idealCycleTimeSeconds / 60m) / runMinutes);

        if (plannedMinutes > 0 && totalCount > 0)
            quality = OeeMath.Round4((double)(goodCount / totalCount));

        double? oee = availability.HasValue && performance.HasValue && quality.HasValue
            ? OeeMath.Round4(availability.Value * performance.Value * quality.Value)
            : null;

        return new OeeSnapshotResponse(
            machineId,
            bucketFrom,
            bucketTo,
            idealCycleTimeSeconds,
            availability,
            performance,
            quality,
            oee,
            availability.HasValue,
            performance.HasValue,
            quality.HasValue,
            plannedMinutes,
            runMinutes,
            downtimeMinutes,
            totalCount,
            goodCount,
            scrapCount,
            idealCycleTimeSource);
    }
}
