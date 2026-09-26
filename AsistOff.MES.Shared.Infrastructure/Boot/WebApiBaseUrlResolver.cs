namespace AsistOff.MES.Shared.Infrastructure.Boot;

/// <summary>
/// Resolves the browser-reachable API base URL for the Web container
/// (issue #271). This is the server-side mirror of the contract implemented
/// by <c>AsistOff.MES.Web/docker-entrypoint.sh</c> and consumed by
/// <c>src/services/apiBaseUrl.ts</c>: the <c>API_BASE_URL</c> environment
/// value wins, then the build-time fallback, then the documented default.
/// An explicitly emptied value (or no usable value at all) is a fail-fast
/// configuration error, never a silent call to the wrong backend.
/// </summary>
public static class WebApiBaseUrlResolver
{
    /// <summary>Runtime override read by the web entrypoint.</summary>
    public const string EnvironmentVariableName = "API_BASE_URL";

    /// <summary>
    /// Documented default: the API port exposed by the compose stack.
    /// </summary>
    public const string DefaultApiBaseUrl = "http://localhost:8080";

    /// <summary>
    /// Resolves the effective URL from the process environment.
    /// </summary>
    public static string Resolve() =>
        Resolve(Environment.GetEnvironmentVariable(EnvironmentVariableName), DefaultApiBaseUrl);

    /// <summary>
    /// Resolves the effective URL: <paramref name="environmentValue"/> wins
    /// over <paramref name="fallbackValue"/> (build-time
    /// <c>VITE_API_BASE_URL</c> or <see cref="DefaultApiBaseUrl"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no usable value remains: the operator emptied the variable
    /// or removed the default.
    /// </exception>
    public static string Resolve(string? environmentValue, string? fallbackValue = DefaultApiBaseUrl)
    {
        var environment = environmentValue?.Trim();
        if (!string.IsNullOrWhiteSpace(environment))
        {
            return environment;
        }

        var fallback = fallbackValue?.Trim();
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        throw new InvalidOperationException(
            $"{EnvironmentVariableName} is not configured and no default is available. " +
            $"Set the {EnvironmentVariableName} environment variable for the web container " +
            $"(example: {EnvironmentVariableName}=https://mes.example.com) or rebuild with VITE_API_BASE_URL. " +
            "See docs/production-runbook.md.");
    }
}
