using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Oee.Summary;

internal sealed class GetOeeSummaryRequestHandler(
    IMachinesRepository machinesRepository,
    IWorkCenterCalendarsRepository calendarsRepository,
    IDowntimeEventsRepository downtimeEventsRepository,
    IProductionConfirmationsRepository confirmationsRepository,
    IProductionOrdersRepository ordersRepository,
    IOperationNodesRepository operationsRepository)
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

        // Performance (slice 3): the ideal cycle time is the minimum positive
        // RunTimePerUnitSeconds across the recipe versions of the confirmed
        // Production Orders in the window. All lookups run under the tenant
        // global query filter, so cross-tenant orders and operations stay
        // invisible and surface as a null ideal (never foreign data).
        decimal? idealCycleTimeSeconds = null;
        if (totalCount > 0)
            idealCycleTimeSeconds = await ResolveIdealCycleTimeSecondsAsync(confirmations, cancellationToken);

        // Null rules: unknown ideal, zero run time or zero total count ->
        // null Performance (never zero). Raw values above 1 (over-cycle: ideal
        // overstated or unrecorded stops) clamp at 1 — the ideal is defined as
        // the fastest sustainable per-unit time.
        double? performance = null;
        if (idealCycleTimeSeconds.HasValue && runMinutes > 0 && totalCount > 0)
        {
            var raw = (double)(totalCount * idealCycleTimeSeconds.Value / 60m) / runMinutes;
            performance = OeeMath.Round4(Math.Min(1.0, raw));
        }

        // Composite OEE: null when any factor is null (never zero).
        double? oee = availability.HasValue && performance.HasValue && quality.HasValue
            ? OeeMath.Round4(availability.Value * performance.Value * quality.Value)
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
            availability,
            idealCycleTimeSeconds,
            performance,
            oee);
    }

    private async Task<decimal?> ResolveIdealCycleTimeSecondsAsync(
        IReadOnlyCollection<ProductionConfirmation> confirmations,
        CancellationToken cancellationToken)
    {
        var versionIds = new HashSet<Guid>();
        foreach (var orderId in confirmations.Select(c => c.ProductionOrderId).Distinct())
        {
            var order = await ordersRepository.GetAsync(orderId, cancellationToken);
            if (order is not null)
                versionIds.Add(order.RecipeVersionId);
        }

        decimal? best = null;
        foreach (var versionId in versionIds)
        {
            var operations = await operationsRepository.ListForVersionAsync(versionId, cancellationToken);
            foreach (var operation in operations)
            {
                if (operation.RunTimePerUnitSeconds is { } seconds && seconds > 0
                    && (best is null || seconds < best))
                    best = seconds;
            }
        }

        return best;
    }
}
