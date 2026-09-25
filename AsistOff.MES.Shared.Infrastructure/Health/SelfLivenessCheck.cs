using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AsistOff.MES.Shared.Infrastructure.Health;

/// <summary>
/// Dependency-free liveness check. Always healthy: it proves the process is
/// running and serving traffic without touching PostgreSQL, so
/// <c>GET /health/live</c> stays green even during a database outage.
/// </summary>
public sealed class SelfLivenessCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HealthCheckResult.Healthy("Process is running."));
}
