using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Oee.Losses;

internal sealed class GetOeeLossesRequestHandler(
    IMachinesRepository machinesRepository,
    IDowntimeEventsRepository downtimeEventsRepository,
    IScrapEventsRepository scrapEventsRepository,
    IReasonCodesRepository reasonCodesRepository)
    : IRequestHandler<GetOeeLossesRequest, OeeLossesResponse>
{
    private const double MaxWindowDays = 93;

    public async Task<OeeLossesResponse> Handle(GetOeeLossesRequest request, CancellationToken cancellationToken)
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

        // The global tenant query filter scopes the machine lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var downtimes = await downtimeEventsRepository.ListOverlappingAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);
        var scraps = await scrapEventsRepository.ListForMachineInWindowAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        // Only closed-event overlap counts, clipped to the window — the same
        // rule as the (1/3) snapshot, so Pareto minutes sum to the snapshot
        // run-time loss. Open events are ignored.
        var downtimeByReason = downtimes
            .Where(e => e.EndedAt.HasValue)
            .GroupBy(e => e.ReasonCodeId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => OeeMath.OverlapMinutes(e.StartedAt, e.EndedAt!.Value, fromUtc, toUtc)));

        var scrapByReason = scraps
            .GroupBy(e => e.ReasonCodeId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity));

        var totalDowntime = downtimeByReason.Values.Sum();
        var totalScrap = scrapByReason.Values.Sum();

        // Tenant-filtered lookup: ids owned by another tenant (or deleted
        // codes) are silently absent, so entries never leak cross-tenant
        // codes. Unresolvable ids still keep their values with null names so
        // Pareto rows always sum to the totals.
        var reasonIds = downtimeByReason.Keys.Concat(scrapByReason.Keys).Distinct().ToList();
        var codes = await reasonCodesRepository.ListByIdsAsync(reasonIds, cancellationToken);
        var codesById = codes.ToDictionary(c => c.Id);

        var downtimePareto = downtimeByReason
            .Select(kvp => BuildDowntimeEntry(kvp.Key, kvp.Value, totalDowntime, codesById))
            .OrderByDescending(e => e.Minutes)
            .ThenBy(e => e.Code ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(e => e.ReasonCodeId)
            .ToList();

        var scrapPareto = scrapByReason
            .Select(kvp => BuildScrapEntry(kvp.Key, kvp.Value, totalScrap, codesById))
            .OrderByDescending(e => e.Quantity)
            .ThenBy(e => e.Code ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(e => e.ReasonCodeId)
            .ToList();

        return new OeeLossesResponse(
            request.MachineId,
            fromUtc,
            toUtc,
            totalDowntime,
            downtimePareto,
            totalScrap,
            scrapPareto);
    }

    private static DowntimeParetoEntry BuildDowntimeEntry(
        Guid reasonCodeId,
        double minutes,
        double total,
        IReadOnlyDictionary<Guid, ReasonCode> codesById)
    {
        codesById.TryGetValue(reasonCodeId, out var code);
        return new DowntimeParetoEntry(
            reasonCodeId,
            code?.Code,
            code?.Name,
            minutes,
            total > 0 ? OeeMath.Round4(minutes / total) : 0);
    }

    private static ScrapParetoEntry BuildScrapEntry(
        Guid reasonCodeId,
        decimal quantity,
        decimal total,
        IReadOnlyDictionary<Guid, ReasonCode> codesById)
    {
        codesById.TryGetValue(reasonCodeId, out var code);
        return new ScrapParetoEntry(
            reasonCodeId,
            code?.Code,
            code?.Name,
            quantity,
            total > 0 ? OeeMath.Round4((double)(quantity / total)) : 0);
    }
}
