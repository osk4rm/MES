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
    IProductionConfirmationsRepository confirmationsRepository)
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

        if (request.IdealCycleTimeSeconds <= 0)
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

        var entries = new List<OeeSnapshotResponse>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
        {
            var (bucketFrom, bucketTo) = buckets[i];
            var isLast = i == buckets.Count - 1;
            entries.Add(BuildSnapshot(
                request.MachineId,
                calendar?.Entries,
                closedDowntimes,
                confirmations,
                bucketFrom,
                bucketTo,
                isLast,
                request.IdealCycleTimeSeconds));
        }

        return new OeeTrendResponse(
            request.MachineId,
            fromUtc,
            toUtc,
            request.IdealCycleTimeSeconds,
            bucket.ToString(),
            entries);
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
        decimal idealCycleTimeSeconds)
    {
        var plannedMinutes = OeeMath.PlannedMinutes(entries, bucketFrom, bucketTo);

        var downtimeMinutes = closedDowntimes
            .Sum(e => OeeMath.OverlapMinutes(e.StartedAt, e.EndedAt!.Value, bucketFrom, bucketTo));

        var runMinutes = Math.Max(0, plannedMinutes - downtimeMinutes);

        // Half-open buckets except the trailing edge: a confirmation stamped
        // exactly on an interior boundary belongs to the later bucket only,
        // so the union of buckets equals the inclusive full-window query.
        var inBucket = confirmations.Where(c =>
            c.ReportedAt >= bucketFrom && (c.ReportedAt < bucketTo || (isLast && c.ReportedAt <= bucketTo)));

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
            scrapCount);
    }
}
