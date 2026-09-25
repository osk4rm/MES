using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AsistOff.MES.Shared.Infrastructure.Health;

/// <summary>
/// Well-known names, tags and paths for the Gateway health probes (issue #249).
/// Orchestrators poll <see cref="LivePath"/> for liveness (process is running,
/// no dependencies) and <see cref="ReadyPath"/> for readiness (PostgreSQL is
/// reachable). <see cref="AliasPath"/> keeps the historical single endpoint
/// working and reflects readiness.
/// </summary>
public static class HealthProbes
{
    /// <summary>Name of the dependency-free liveness check.</summary>
    public const string SelfCheckName = "self";

    /// <summary>Name of the PostgreSQL readiness check.</summary>
    public const string PostgresCheckName = "postgres";

    /// <summary>Tag selecting the dependency-free liveness check.</summary>
    public const string LiveTag = "live";

    /// <summary>Tag selecting the PostgreSQL readiness check.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Liveness probe path: 200 when the process serves traffic.</summary>
    public const string LivePath = "/health/live";

    /// <summary>Readiness probe path: 200 when PostgreSQL is reachable, 503 otherwise.</summary>
    public const string ReadyPath = "/health/ready";

    /// <summary>Historical endpoint, kept as a readiness alias for existing callers.</summary>
    public const string AliasPath = "/health";

    /// <summary>Predicate for the liveness endpoint: only checks tagged <c>live</c> run.</summary>
    public static bool IsLiveCheck(HealthCheckRegistration registration)
        => registration.Tags.Contains(LiveTag);

    /// <summary>Predicate for the readiness endpoint and its alias: only checks tagged <c>ready</c> run.</summary>
    public static bool IsReadyCheck(HealthCheckRegistration registration)
        => registration.Tags.Contains(ReadyTag);
}
