using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Reliability.Trend;

internal sealed class GetReliabilityTrendRequestHandler(
    IMachinesRepository machinesRepository,
    IDowntimeEventsRepository downtimeEventsRepository,
    IMaintenanceWorkOrdersRepository maintenanceWorkOrdersRepository)
    : IRequestHandler<GetReliabilityTrendRequest, ReliabilityTrendResponse>
{
    private const double MaxWindowDays = 93;

    public async Task<ReliabilityTrendResponse> Handle(
        GetReliabilityTrendRequest request, CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(request.Bucket)
            || !Enum.TryParse<ReliabilityTrendBucket>(request.Bucket, ignoreCase: true, out var bucket)
            || !Enum.IsDefined(bucket))
            throw new ValidationException(nameof(request.Bucket), "Bucket must be either Day or Week.");

        // The global tenant query filter scopes the machine lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        // Single full-window fetch, sliced per bucket in memory: the overlap
        // of an event with a bucket is contained in its overlap with the
        // window, and a repair belongs to exactly one bucket by CompletedAt,
        // so per-bucket math equals a per-bucket snapshot query.
        var downtimes = await downtimeEventsRepository.ListOverlappingAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);
        var repairs = await maintenanceWorkOrdersRepository.ListDoneInWindowAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        // Only closed-event overlap counts; open events are ignored.
        var closedDowntimes = downtimes.Where(e => e.EndedAt.HasValue).ToList();

        // The repository already filters Done + CompletedAt in window; ignore
        // non-Done rows defensively so they never count.
        var doneRepairs = repairs
            .Where(r => r.Status == MaintenanceWorkOrderStatus.Done && r.CompletedAt.HasValue)
            .ToList();

        var buckets = BuildBuckets(bucket, fromUtc, toUtc);

        var entries = new List<ReliabilitySnapshotResponse>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
        {
            var (bucketFrom, bucketTo) = buckets[i];
            var isLast = i == buckets.Count - 1;
            entries.Add(BuildSnapshot(
                request.MachineId,
                closedDowntimes,
                doneRepairs,
                bucketFrom,
                bucketTo,
                isLast));
        }

        return new ReliabilityTrendResponse(
            request.MachineId,
            fromUtc,
            toUtc,
            bucket.ToString(),
            entries);
    }

    /// <summary>
    /// Partitions <c>[fromUtc, toUtc)</c> into calendar-aligned buckets in
    /// ascending order. Day buckets break at UTC midnights, week buckets at
    /// Monday 00:00 UTC; edge buckets are clipped to the query window.
    /// </summary>
    internal static IReadOnlyList<(DateTime From, DateTime To)> BuildBuckets(
        ReliabilityTrendBucket bucket, DateTime fromUtc, DateTime toUtc)
    {
        var result = new List<(DateTime From, DateTime To)>();

        DateTime cursor = bucket == ReliabilityTrendBucket.Day
            ? fromUtc.Date
            : fromUtc.Date.AddDays(-DaysSinceMonday(fromUtc.Date));
        var stepDays = bucket == ReliabilityTrendBucket.Day ? 1 : 7;

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
    /// Snapshot-equivalent computation for one bucket: failure count and
    /// downtime from closed-event overlap with the bucket (open events
    /// ignored), repair count and average repair time from Done rows whose
    /// <c>CompletedAt</c> falls in the bucket. Null rules mirror
    /// <c>GetReliabilitySnapshotRequestHandler</c> exactly.
    /// </summary>
    private static ReliabilitySnapshotResponse BuildSnapshot(
        Guid machineId,
        IReadOnlyCollection<DowntimeEvent> closedDowntimes,
        IReadOnlyCollection<MaintenanceWorkOrder> doneRepairs,
        DateTime bucketFrom,
        DateTime bucketTo,
        bool isLast)
    {
        var overlaps = closedDowntimes
            .Select(e => GetReliabilitySnapshotRequestHandler.OverlapMinutes(
                e.StartedAt, e.EndedAt!.Value, bucketFrom, bucketTo))
            .Where(overlap => overlap > 0)
            .ToList();

        var failureCount = overlaps.Count;
        var totalDowntimeMinutes = GetReliabilitySnapshotRequestHandler.Round2(overlaps.Sum());

        var windowMinutes = (bucketTo - bucketFrom).TotalMinutes;
        var uptimeMinutes = windowMinutes - totalDowntimeMinutes;

        double? mtbfMinutes = failureCount > 0
            ? GetReliabilitySnapshotRequestHandler.Round2(uptimeMinutes / failureCount)
            : null;
        double? mttrMinutes = failureCount > 0
            ? GetReliabilitySnapshotRequestHandler.Round2(totalDowntimeMinutes / failureCount)
            : null;

        // Half-open buckets except the trailing edge: a repair completed
        // exactly on an interior boundary belongs to the later bucket only,
        // so the union of buckets equals the inclusive full-window query.
        var inBucket = doneRepairs.Where(r =>
            r.CompletedAt!.Value >= bucketFrom
            && (r.CompletedAt!.Value < bucketTo || (isLast && r.CompletedAt!.Value <= bucketTo)));

        var inBucketList = inBucket.ToList();
        var repairCount = inBucketList.Count;
        double? avgRepairMinutes = repairCount > 0
            ? GetReliabilitySnapshotRequestHandler.Round2(inBucketList.Average(r =>
                (r.CompletedAt!.Value - (r.StartedAt ?? r.ReportedAt)).TotalMinutes))
            : null;

        return new ReliabilitySnapshotResponse(
            machineId,
            bucketFrom,
            bucketTo,
            failureCount,
            repairCount,
            windowMinutes,
            uptimeMinutes,
            totalDowntimeMinutes,
            mtbfMinutes,
            mttrMinutes,
            avgRepairMinutes);
    }
}
