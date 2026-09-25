using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Oee.Summary;

internal sealed class GetOeeSummaryRequestHandler(
    IMachinesRepository machinesRepository,
    IWorkCenterCalendarsRepository calendarsRepository,
    IDowntimeEventsRepository downtimeEventsRepository,
    IProductionConfirmationsRepository confirmationsRepository)
    : IRequestHandler<GetOeeSummaryRequest, OeeSummaryResponse>
{
    public async Task<OeeSummaryResponse> Handle(GetOeeSummaryRequest request, CancellationToken cancellationToken)
    {
        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");

        if (request.FromUtc == default || request.ToUtc == default)
            throw new ValidationException(nameof(request.FromUtc), "Time window is required.");

        var fromUtc = request.FromUtc.ToUniversalTime();
        var toUtc = request.ToUtc.ToUniversalTime();

        if (fromUtc >= toUtc)
            throw new ValidationException(nameof(request.FromUtc), "FromUtc must be before ToUtc.");

        // The global tenant query filter scopes the machine lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var confirmations = await confirmationsRepository.ListForMachineInWindowAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        // Quality uses confirmations only, so ScrapEvent rows never double count.
        var goodCount = confirmations.Sum(c => c.GoodQuantity);
        var scrapCount = confirmations.Sum(c => c.ScrapQuantity);
        var totalCount = goodCount + scrapCount;

        double? quality = totalCount > 0
            ? OeeMath.Round4((double)(goodCount / totalCount))
            : null;

        // Availability (slice 2): planned time is the overlap of the Work
        // Center calendar Shifts with the window; downtime sums closed-event
        // overlap only. Both repositories run under the tenant global query
        // filter, so cross-tenant calendars and downtime stay invisible.
        var calendar = await calendarsRepository.GetByMachineIdAsync(request.MachineId, cancellationToken);
        var plannedMinutes = OeeMath.PlannedMinutes(calendar?.Entries, fromUtc, toUtc);

        var downtimes = await downtimeEventsRepository.ListOverlappingAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        // Only closed-event overlap counts; open events are ignored.
        var downtimeMinutes = downtimes
            .Where(e => e.EndedAt.HasValue)
            .Sum(e => OeeMath.OverlapMinutes(e.StartedAt, e.EndedAt!.Value, fromUtc, toUtc));

        // Clamp: a stop overlapping mostly unplanned time must never drive
        // run time negative.
        var runMinutes = Math.Max(0, plannedMinutes - downtimeMinutes);

        // Null rule: no planned time -> null Availability (never zero).
        // Full downtime yields 0, not null: run time zero is computed.
        double? availability = plannedMinutes > 0
            ? OeeMath.Round4(runMinutes / plannedMinutes)
            : null;

        return new OeeSummaryResponse(
            request.MachineId,
            fromUtc,
            toUtc,
            goodCount,
            scrapCount,
            totalCount,
            quality,
            plannedMinutes,
            runMinutes,
            downtimeMinutes,
            availability);
    }
}
