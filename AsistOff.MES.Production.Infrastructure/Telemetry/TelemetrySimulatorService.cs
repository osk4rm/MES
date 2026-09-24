using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Application.Telemetry;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Production.Infrastructure.Telemetry;

/// <summary>
/// Schedule-driven simulator poller that stands in for real OPC UA hardware.
/// Every <c>Telemetry:SimulatorIntervalSeconds</c> seconds it iterates all
/// active tenants and appends one <c>Quality=Good</c> reading per enabled tag
/// (random walk within the tag data type) through the shared
/// <see cref="ITelemetryIngestionService"/>, so manual Submit and simulated
/// values obey identical rules.
///
/// Disabled while <c>Telemetry:SimulatorEnabled</c> is <c>false</c> (the
/// production default). Each tenant is processed inside its own
/// <see cref="BackgroundTenantContext"/> scope, so the EF Core global query
/// filter isolates tenant data with no <c>IgnoreQueryFilters</c> anywhere.
/// A failure on one tag is logged and the batch continues with the rest.
/// </summary>
public sealed class TelemetrySimulatorService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<TelemetryOptions> telemetryOptions,
    IDateTimeProvider dateTimeProvider,
    ILogger<TelemetrySimulatorService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = Math.Clamp(telemetryOptions.CurrentValue.SimulatorIntervalSeconds, 1, 3600);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Telemetry simulator poll failed");
            }
        }
    }

    /// <summary>
    /// Runs a single poll cycle across all active tenants. Public so tests
    /// can drive the poller without waiting for the interval timer.
    /// Returns the number of readings appended.
    /// </summary>
    public async Task<int> PollOnceAsync(CancellationToken cancellationToken = default)
    {
        var options = telemetryOptions.CurrentValue;
        if (!options.SimulatorEnabled)
            return 0;

        var now = dateTimeProvider.UtcNow;
        var random = new Random();
        var total = 0;

        using var rootScope = scopeFactory.CreateScope();
        var tenantIds = await rootScope.ServiceProvider
            .GetRequiredService<AsistOff.MES.Multitenancy.Repositories.ITenantRepository>()
            .ListActiveIdsAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The ambient scope makes the tenant query filter resolve exactly
            // this tenant for every repository call below.
            using (BackgroundTenantContext.BeginScope(tenantId))
            using (var scope = scopeFactory.CreateScope())
            {
                var provider = scope.ServiceProvider;
                total += await PollTenantAsync(
                    provider.GetRequiredService<IMachineTelemetryTagsRepository>(),
                    provider.GetRequiredService<ITelemetryReadingsRepository>(),
                    provider.GetRequiredService<ITelemetryIngestionService>(),
                    tenantId,
                    now,
                    random,
                    logger,
                    cancellationToken);
            }
        }

        return total;
    }

    /// <summary>
    /// Polls every enabled tag of one tenant. Disabled tags are skipped and a
    /// per-tag failure is logged without aborting the remaining tags.
    /// Returns the number of readings appended.
    /// </summary>
    internal static async Task<int> PollTenantAsync(
        IMachineTelemetryTagsRepository tagsRepository,
        ITelemetryReadingsRepository readingsRepository,
        ITelemetryIngestionService ingestionService,
        Guid tenantId,
        DateTime now,
        Random random,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var tags = await tagsRepository.ListEnabledAsync(cancellationToken);
        var written = 0;

        foreach (var tag in tags)
        {
            if (!tag.IsEnabled)
                continue;

            try
            {
                var previous = await readingsRepository.GetLatestAsync(tag.Id, cancellationToken);
                var (doubleValue, stringValue) = BuildNextValue(tag, previous, random);

                await ingestionService.IngestAsync(
                    tag.Id, now, doubleValue, stringValue, TelemetryQuality.Good, tenantId, cancellationToken);
                written++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Telemetry simulator skipped tag {TagId}", tag.Id);
            }
        }

        return written;
    }

    /// <summary>
    /// Random walk seeded by the previous reading (or a neutral default when
    /// the tag never reported). Numeric and boolean values travel in the
    /// double slot; text tags repeat a steady heartbeat value.
    /// </summary>
    internal static (double? DoubleValue, string? StringValue) BuildNextValue(
        MachineTelemetryTag tag, TelemetryReading? previous, Random random)
    {
        return tag.DataType switch
        {
            TelemetryDataType.Boolean => ((double?)(previous?.DoubleValue >= 0.5 ? 0.0 : 1.0), null),
            TelemetryDataType.Integer => ((double?)Math.Round((previous?.DoubleValue ?? 0.0) + random.Next(-1, 2)), null),
            TelemetryDataType.String => (null, previous?.StringValue ?? "sim-ok"),
            _ => ((double?)(previous?.DoubleValue ?? 20.0) + (random.NextDouble() * 2.0 - 1.0), null),
        };
    }
}
