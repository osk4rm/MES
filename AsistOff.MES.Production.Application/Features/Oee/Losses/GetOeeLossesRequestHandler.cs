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

        // Closed-event overlap only, mirroring the (1/3) snapshot run-time
        // loss; open events are ignored.
        var downtimeByReason = downtimes
            .Where(e => e.EndedAt.HasValue)
            .GroupBy(e => e.ReasonCodeId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => OeeMath.OverlapMinutes(e.StartedAt, e.EndedAt!.Value, fromUtc, toUtc)));
        var totalDowntime = downtimeByReason.Values.Sum();

        var scrapByReason = scraps
            .GroupBy(s => s.ReasonCodeId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));
        var totalScrap = scrapByReason.Values.Sum();

        // Resolve codes/names strictly through the tenant-filtered dictionary:
        // GetByIdAsync never sees other tenants' rows, so a foreign or deleted
        // reason surfaces as null and falls back to "Unknown" instead of
        // leaking a cross-tenant code.
        var reasonIds = downtimeByReason.Keys.Concat(scrapByReason.Keys).Distinct().ToList();
        var reasons = new Dictionary<Guid, (string Code, string Name)>();
        foreach (var reasonId in reasonIds)
        {
            var reason = await reasonCodesRepository.GetByIdAsync(reasonId, cancellationToken);
            reasons[reasonId] = reason is null
                ? ("Unknown", "Unknown")
                : (reason.Code, reason.Name);
        }

        var downtimePareto = downtimeByReason
            .Select(kv => new OeeDowntimeParetoEntry(
                kv.Key,
                reasons[kv.Key].Code,
                reasons[kv.Key].Name,
                kv.Value,
                totalDowntime > 0 ? OeeMath.Round4(kv.Value / totalDowntime) : 0))
            .OrderByDescending(e => e.Minutes)
            .ThenBy(e => e.Code, StringComparer.Ordinal)
            .ThenBy(e => e.ReasonCodeId)
            .ToList();

        var scrapPareto = scrapByReason
            .Select(kv => new OeeScrapParetoEntry(
                kv.Key,
                reasons[kv.Key].Code,
                reasons[kv.Key].Name,
                kv.Value,
                totalScrap > 0 ? OeeMath.Round4((double)(kv.Value / totalScrap)) : 0))
            .OrderByDescending(e => e.Quantity)
            .ThenBy(e => e.Code, StringComparer.Ordinal)
            .ThenBy(e => e.ReasonCodeId)
            .ToList();

        return new OeeLossesResponse(
            request.MachineId,
            fromUtc,
            toUtc,
            totalDowntime,
            totalScrap,
            downtimePareto,
            scrapPareto);
    }
}
