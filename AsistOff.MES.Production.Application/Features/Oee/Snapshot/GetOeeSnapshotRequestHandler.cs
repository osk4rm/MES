using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Oee.Snapshot;

internal sealed class GetOeeSnapshotRequestHandler(
    IMachinesRepository machinesRepository,
    IWorkCenterCalendarsRepository calendarsRepository,
    IDowntimeEventsRepository downtimeEventsRepository,
    IProductionConfirmationsRepository confirmationsRepository)
    : IRequestHandler<GetOeeSnapshotRequest, OeeSnapshotResponse>
{
    private const double MaxWindowDays = 93;

    public async Task<OeeSnapshotResponse> Handle(GetOeeSnapshotRequest request, CancellationToken cancellationToken)
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

        // The global tenant query filter scopes the machine lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var calendar = await calendarsRepository.GetByMachineIdAsync(request.MachineId, cancellationToken);
        var plannedMinutes = OeeMath.PlannedMinutes(calendar?.Entries, fromUtc, toUtc);

        var downtimes = await downtimeEventsRepository.ListOverlappingAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        // Only closed-event overlap counts; open events are ignored.
        var downtimeMinutes = downtimes
            .Where(e => e.EndedAt.HasValue)
            .Sum(e => OeeMath.OverlapMinutes(e.StartedAt, e.EndedAt!.Value, fromUtc, toUtc));

        // Clamp: a stop overlapping mostly unplanned time must never drive
        // run time negative; zero run time surfaces as null factors below.
        var runMinutes = Math.Max(0, plannedMinutes - downtimeMinutes);

        var confirmations = await confirmationsRepository.ListForMachineInWindowAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        // Quality uses confirmations only, so ScrapEvent rows never double count.
        var goodCount = confirmations.Sum(c => c.GoodQuantity);
        var scrapCount = confirmations.Sum(c => c.ScrapQuantity);
        var totalCount = goodCount + scrapCount;

        // Null rules: no planned time -> all null; zero run time ->
        // Availability and Performance null; zero total count ->
        // Performance and Quality null.
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

        return new OeeSnapshotResponse(
            request.MachineId,
            fromUtc,
            toUtc,
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
            scrapCount);
    }
}

/// <summary>
/// Pure OEE interval math: half-open overlap of two instants and expansion
/// of the recurring weekly calendar entries into the query window.
/// </summary>
internal static class OeeMath
{
    internal static double OverlapMinutes(DateTime start, DateTime end, DateTime from, DateTime to)
    {
        var overlapStart = start > from ? start : from;
        var overlapEnd = end < to ? end : to;

        return overlapEnd > overlapStart ? (overlapEnd - overlapStart).TotalMinutes : 0;
    }

    internal static double PlannedMinutes(
        IEnumerable<WorkCenterCalendarEntry>? entries, DateTime fromUtc, DateTime toUtc)
    {
        if (entries is null)
            return 0;

        var total = 0.0;

        // Start one day early: an overnight entry that starts the previous
        // day can still spill into the window.
        for (var day = fromUtc.Date.AddDays(-1); day <= toUtc.Date; day = day.AddDays(1))
        {
            foreach (var entry in entries)
            {
                if (!entry.IsWorking || entry.DayOfWeek != day.DayOfWeek)
                    continue;

                var start = day.Add(entry.StartTime.ToTimeSpan());

                // Half-open [StartTime, EndTime); EndTime not after StartTime
                // crosses midnight into the following day.
                var end = entry.EndTime > entry.StartTime
                    ? day.Add(entry.EndTime.ToTimeSpan())
                    : day.AddDays(1).Add(entry.EndTime.ToTimeSpan());

                total += OverlapMinutes(start, end, fromUtc, toUtc);
            }
        }

        return total;
    }

    internal static double Round4(double value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
