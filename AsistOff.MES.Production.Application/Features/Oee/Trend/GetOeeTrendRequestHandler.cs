using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
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

        var bucket = OeeBuckets.Normalize(request.Bucket);

        // The global tenant query filter scopes the machine lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var calendar = await calendarsRepository.GetByMachineIdAsync(request.MachineId, cancellationToken);

        // Single fetch for the whole window, then sliced per bucket in memory.
        // Bucket overlap sums partition the window without gaps, so each entry
        // is mathematically identical to a (1/3) snapshot over that bucket;
        // confirmations are assigned half-open [start, end) except the last
        // bucket which keeps the snapshot inclusive upper bound.
        var downtimes = await downtimeEventsRepository.ListOverlappingAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);
        var confirmations = await confirmationsRepository.ListForMachineInWindowAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        var buckets = OeeBuckets.Build(fromUtc, toUtc, bucket);
        var entries = new List<OeeSnapshotResponse>(buckets.Count);

        for (var i = 0; i < buckets.Count; i++)
        {
            var (bucketFrom, bucketTo) = buckets[i];
            var isLast = i == buckets.Count - 1;

            var plannedMinutes = OeeMath.PlannedMinutes(calendar?.Entries, bucketFrom, bucketTo);
            var downtimeMinutes = downtimes
                .Where(e => e.EndedAt.HasValue)
                .Sum(e => OeeMath.OverlapMinutes(e.StartedAt, e.EndedAt!.Value, bucketFrom, bucketTo));
            var runMinutes = Math.Max(0, plannedMinutes - downtimeMinutes);

            var inBucket = confirmations.Where(c =>
                c.ReportedAt >= bucketFrom && (c.ReportedAt < bucketTo || (isLast && c.ReportedAt <= bucketTo)));

            var goodCount = inBucket.Sum(c => c.GoodQuantity);
            var scrapCount = inBucket.Sum(c => c.ScrapQuantity);
            var totalCount = goodCount + scrapCount;

            // Null rules mirror the (1/3) snapshot handler exactly.
            double? availability = null;
            double? performance = null;
            double? quality = null;

            if (plannedMinutes > 0 && runMinutes > 0)
                availability = OeeMath.Round4(runMinutes / plannedMinutes);

            if (plannedMinutes > 0 && runMinutes > 0 && totalCount > 0)
                performance = OeeMath.Round4((double)(totalCount * request.IdealCycleTimeSeconds / 60m) / runMinutes);

            if (plannedMinutes > 0 && totalCount > 0)
                quality = OeeMath.Round4((double)(goodCount / totalCount));

            double? oee = availability.HasValue && performance.HasValue && quality.HasValue
                ? OeeMath.Round4(availability.Value * performance.Value * quality.Value)
                : null;

            entries.Add(new OeeSnapshotResponse(
                request.MachineId,
                bucketFrom,
                bucketTo,
                request.IdealCycleTimeSeconds,
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
                scrapCount));
        }

        return new OeeTrendResponse(
            request.MachineId,
            fromUtc,
            toUtc,
            bucket,
            request.IdealCycleTimeSeconds,
            entries);
    }
}

/// <summary>
/// Bucket partitioning for the OEE trend: <c>Day</c> buckets split at UTC
/// midnight, <c>Week</c> buckets split at Monday 00:00 UTC. Buckets are
/// half-open, contiguous and ascending, covering exactly [from, to).
/// </summary>
internal static class OeeBuckets
{
    internal const string Day = "Day";
    internal const string Week = "Week";

    internal static string Normalize(string? bucket)
    {
        if (string.Equals(bucket, Day, StringComparison.OrdinalIgnoreCase))
            return Day;

        if (string.Equals(bucket, Week, StringComparison.OrdinalIgnoreCase))
            return Week;

        throw new ValidationException(nameof(GetOeeTrendRequest.Bucket), "Bucket must be Day or Week.");
    }

    internal static IReadOnlyList<(DateTime From, DateTime To)> Build(DateTime fromUtc, DateTime toUtc, string bucket)
    {
        var result = new List<(DateTime From, DateTime To)>();
        var cursor = fromUtc;

        while (cursor < toUtc)
        {
            DateTime boundary = bucket == Week ? NextMondayMidnight(cursor) : cursor.Date.AddDays(1);
            var end = boundary < toUtc ? boundary : toUtc;
            result.Add((cursor, end));
            cursor = end;
        }

        return result;
    }

    private static DateTime NextMondayMidnight(DateTime cursor)
    {
        var midnight = cursor.Date;

        // Exactly at a Monday midnight the current week still has 7 days left.
        if (cursor == midnight && midnight.DayOfWeek == DayOfWeek.Monday)
            return midnight.AddDays(7);

        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)midnight.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
            daysUntilMonday = 7;

        return midnight.AddDays(daysUntilMonday);
    }
}
