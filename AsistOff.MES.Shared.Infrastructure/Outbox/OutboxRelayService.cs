using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Shared.Infrastructure.Outbox;

/// <summary>
/// Slice 2 (#259) background relay for the transactional outbox. Every
/// <c>Outbox:PollIntervalSeconds</c> seconds it lists active tenants via
/// <see cref="IOutboxTenantSource"/> and dispatches each tenant's
/// undispatched rows (bounded by <c>Outbox:BatchSize</c>, oldest first)
/// through MediatR — strictly after the staging transactions committed, so
/// ghost events on rollback are impossible by construction: the relay only
/// ever reads committed rows.
///
/// Tenant isolation: each tenant is processed inside its own
/// <see cref="BackgroundTenantContext"/> scope with a fresh DI scope, so the
/// EF Core global query filter resolves exactly that tenant for every query
/// and every dispatch runs under the stored row's tenant. There is no
/// <c>IgnoreQueryFilters</c> bypass anywhere, and a failure in one tenant
/// never aborts the remaining tenants.
///
/// The loop is delay-first (startup, migrations and seeding finish before the
/// first cycle) and stops gracefully on shutdown. <see cref="RelayOnceAsync"/>
/// and <see cref="RelayTenantAsync"/> are explicit entry points so tests can
/// drive the relay deterministically without waiting for the timer; the
/// <c>Outbox:Enabled</c> switch gates only the timer loop.
/// </summary>
public sealed class OutboxRelayService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<OutboxRelayOptions> relayOptions,
    ILogger<OutboxRelayService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!relayOptions.CurrentValue.Enabled)
        {
            logger.LogInformation("Outbox relay is disabled (Outbox:Enabled=false)");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = Math.Clamp(relayOptions.CurrentValue.PollIntervalSeconds, 1, 3600);

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
                await RelayOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox relay cycle failed");
            }
        }
    }

    /// <summary>
    /// Runs a single relay cycle across all active tenants. Public so tests
    /// can drive the relay without waiting for the interval timer. Returns
    /// the number of rows marked dispatched.
    /// </summary>
    public async Task<int> RelayOnceAsync(CancellationToken cancellationToken = default)
    {
        using var rootScope = scopeFactory.CreateScope();
        var tenantIds = await rootScope.ServiceProvider
            .GetRequiredService<IOutboxTenantSource>()
            .ListActiveTenantIdsAsync(cancellationToken);

        var total = 0;

        foreach (var tenantId in tenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                total += await RelayTenantAsync(tenantId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox relay failed for tenant {TenantId}", tenantId);
            }
        }

        return total;
    }

    /// <summary>
    /// Relays a single tenant's undispatched outbox rows under that tenant's
    /// scope. Public so tests can target one tenant without touching other
    /// tenants' rows. Returns the number of rows marked dispatched.
    /// </summary>
    public async Task<int> RelayTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var options = relayOptions.CurrentValue;

        // The ambient scope makes the tenant query filter resolve exactly
        // this tenant for every query below.
        using (BackgroundTenantContext.BeginScope(tenantId))
        using (var scope = scopeFactory.CreateScope())
        {
            return await RelayTenantCoreAsync(
                scope.ServiceProvider,
                options.BatchSize,
                Math.Clamp(options.MaxAttempts, 1, OutboxDispatcher.MaxAttemptsLimit),
                cancellationToken);
        }
    }

    internal static async Task<int> RelayTenantCoreAsync(
        IServiceProvider provider,
        int batchSize,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        var context = provider.GetRequiredService<DefaultContext>();
        var publisher = provider.GetRequiredService<IPublisher>();
        var dispatcher = provider.GetRequiredService<OutboxDispatcher>();
        var relayLogger = provider.GetRequiredService<ILogger<OutboxRelayService>>();

        var rows = await OutboxStager
            .ApplyUndispatched(context.OutboxMessages, batchSize, maxAttempts)
            .ToListAsync(cancellationToken);

        var dispatched = 0;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await dispatcher.DispatchRowAsync(
                    row,
                    publisher,
                    maxAttempts,
                    async ct => await context.SaveChangesAsync(ct),
                    cancellationToken))
                {
                    dispatched++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                relayLogger.LogError(
                    exception,
                    "Outbox relay failed for outbox {OutboxId} of type {EventType}",
                    row.Id,
                    row.Type);
            }
        }

        return dispatched;
    }
}
