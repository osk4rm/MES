using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Telemetry;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Production.Infrastructure.Telemetry;

/// <summary>
/// Background poller that follows the OPC UA connection registry. Every
/// <c>OpcUa:PollingIntervalSeconds</c> seconds it iterates all active tenants
/// and polls every enabled connection through <see cref="IOpcUaReader"/>.
/// On success the connection <c>LastSeenAtUtc</c> is stamped and
/// <c>LastError</c> cleared; on failure <c>LastError</c> records the message
/// (max 512 chars). Disabled connections are skipped and never touched.
/// Each tenant is processed inside its own
/// <see cref="BackgroundTenantContext"/> scope, so the EF Core global query
/// filter isolates tenant data with no <c>IgnoreQueryFilters</c> anywhere.
/// A failure on one connection is recorded and the batch continues.
/// </summary>
public sealed class OpcUaPollingService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<OpcUaPollingOptions> pollingOptions,
    IDateTimeProvider dateTimeProvider,
    ILogger<OpcUaPollingService> logger)
    : BackgroundService
{
    public const int MaxLastErrorLength = 512;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = Math.Clamp(pollingOptions.CurrentValue.PollingIntervalSeconds, 5, 3600);

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
                logger.LogError(exception, "OPC UA poll failed");
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
        var options = pollingOptions.CurrentValue;
        if (!options.PollingEnabled)
            return 0;

        var now = dateTimeProvider.UtcNow;
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
                    provider.GetRequiredService<IOpcUaConnectionsRepository>(),
                    provider.GetRequiredService<IOpcUaReader>(),
                    tenantId,
                    now,
                    logger,
                    cancellationToken);
            }
        }

        return total;
    }

    /// <summary>
    /// Polls every enabled connection of one tenant. Disabled connections are
    /// skipped without any write. A per-connection failure records
    /// <c>LastError</c> without aborting the remaining connections.
    /// Returns the number of readings appended.
    /// </summary>
    internal static async Task<int> PollTenantAsync(
        IOpcUaConnectionsRepository connectionsRepository,
        IOpcUaReader reader,
        Guid tenantId,
        DateTime now,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var connections = await connectionsRepository.ListEnabledAsync(cancellationToken);
        var written = 0;

        foreach (var connection in connections)
        {
            if (!connection.IsEnabled)
                continue;

            try
            {
                written += await reader.PollAsync(connection, tenantId, cancellationToken);

                connection.LastSeenAtUtc = now;
                connection.LastError = null;
                await connectionsRepository.UpdateAsync(connection, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "OPC UA poll failed for connection {ConnectionId}", connection.Id);

                connection.LastError = Truncate(exception.Message);
                await connectionsRepository.UpdateAsync(connection, cancellationToken);
            }
        }

        return written;
    }

    internal static string Truncate(string message)
        => message.Length <= MaxLastErrorLength ? message : message[..MaxLastErrorLength];
}
