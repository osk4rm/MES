using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AsistOff.MES.Shared.Infrastructure.Health;

/// <summary>
/// PostgreSQL readiness check. Opens a connection through the shared
/// <see cref="DefaultContext"/> (resolved per-check from a fresh scope, so it
/// always follows the configured connection string) and reports healthy only
/// when the database answers.
///
/// Failure results are deliberately bare: no exception is attached and no
/// description carries connection details, so neither the probe response body
/// (see <see cref="HealthProbeResponseWriter"/>) nor any log line can leak
/// connection strings or secrets. No logger is injected on purpose — the 503
/// status on the probe is the signal orchestrators act on.
/// </summary>
public sealed class PostgresReadinessHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    /// <summary>Generic failure description; safe to expose over HTTP.</summary>
    public const string UnavailableDescription = "PostgreSQL is unavailable.";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DefaultContext>();

            var reachable = await db.Database.CanConnectAsync(cancellationToken);
            return reachable
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy(UnavailableDescription);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Swallow by design: attaching the exception would risk serializing
            // provider error details (host, username) into probe responses.
            return HealthCheckResult.Unhealthy(UnavailableDescription);
        }
    }
}
