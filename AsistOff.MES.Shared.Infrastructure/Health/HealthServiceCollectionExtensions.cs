using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Infrastructure.Health;

/// <summary>
/// Registers the Gateway health probes: a dependency-free <c>self</c> check
/// tagged <c>live</c> and a PostgreSQL <c>postgres</c> check tagged
/// <c>ready</c>. The Gateway maps them to <c>/health/live</c>,
/// <c>/health/ready</c> and the backwards-compatible <c>/health</c> alias via
/// <see cref="HealthProbes"/> predicates.
/// </summary>
public static class HealthServiceCollectionExtensions
{
    public static IServiceCollection AddMesHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<SelfLivenessCheck>(
                HealthProbes.SelfCheckName,
                tags: new[] { HealthProbes.LiveTag })
            .AddCheck<PostgresReadinessHealthCheck>(
                HealthProbes.PostgresCheckName,
                tags: new[] { HealthProbes.ReadyTag });

        return services;
    }
}
