using AsistOff.MES.Production.Application.Telemetry;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Production.Application.Features.MachineTelemetryTags.Status;

internal sealed class GetTelemetryStatusRequestHandler(
    IMachineTelemetryTagsRepository tagsRepository,
    ITelemetryReadingsRepository readingsRepository,
    IDateTimeProvider dateTimeProvider,
    IOptionsMonitor<TelemetryOptions> telemetryOptions)
    : IRequestHandler<GetTelemetryStatusRequest, TelemetryStatusResponse>
{
    public async Task<TelemetryStatusResponse> Handle(GetTelemetryStatusRequest request, CancellationToken cancellationToken)
    {
        // The global tenant query filter scopes both repositories to the
        // caller tenant, so other-tenant tags never appear here.
        var options = telemetryOptions.CurrentValue;
        var intervalSeconds = Math.Clamp(options.SimulatorIntervalSeconds, 1, 3600);
        var staleAfter = TimeSpan.FromSeconds(2L * intervalSeconds);

        var now = dateTimeProvider.UtcNow;
        var hourAgo = now.AddHours(-1);

        var tags = await tagsRepository.ListAllAsync(cancellationToken);

        var entries = new List<TelemetryTagStatusEntry>(tags.Count);
        foreach (var tag in tags)
        {
            var latest = await readingsRepository.GetLatestAsync(tag.Id, cancellationToken);
            var hourlyCount = await readingsRepository.CountSinceAsync(tag.Id, hourAgo, cancellationToken);

            var stale = latest is not null && now - latest.ReadAt > staleAfter;

            entries.Add(new TelemetryTagStatusEntry(
                tag.Id,
                tag.MachineId,
                tag.NodeId,
                tag.DisplayName,
                tag.IsEnabled,
                latest?.ReadAt,
                hourlyCount,
                stale));
        }

        return new TelemetryStatusResponse(options.SimulatorEnabled, intervalSeconds, entries);
    }
}
