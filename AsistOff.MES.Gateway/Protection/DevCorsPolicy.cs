using System.Net;

namespace AsistOff.MES.Gateway.Protection;

/// <summary>
/// Development-only CORS origin predicate (issue #369). The previous
/// Development fallback reflected <em>any</em> origin together with
/// <c>AllowCredentials</c>, so a dev API run would serve auth cookies to
/// whatever site the operator happened to visit. The fallback now accepts
/// only loopback origins — <c>localhost</c>, <c>127.0.0.1</c>, <c>::1</c>
/// over http/https with any port (the Vite dev servers) — and rejects
/// everything else, including <c>https://evil.test</c>. Explicitly configured
/// <c>cors:allowedOrigins</c> entries are unaffected, and non-Development
/// hosts with an empty list still fail fast at startup (see
/// <see cref="EnsureConfigured"/>).
/// </summary>
public static class DevCorsPolicy
{
    /// <summary>
    /// True only for absolute http/https URIs whose host is a loopback
    /// address. Malformed origins, non-http(s) schemes, and DNS names other
    /// than <c>localhost</c> are rejected.
    /// </summary>
    public static bool IsLoopbackOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    /// <summary>
    /// Returns the effective allowlist: the configured origins when non-empty,
    /// otherwise an empty array for Development (the caller falls back to the
    /// <see cref="IsLoopbackOrigin"/> predicate) or throws for any other
    /// environment, preserving the existing Production fail-fast behavior.
    /// </summary>
    public static string[] EnsureConfigured(string[]? configured, bool isDevelopment)
    {
        if (configured is { Length: > 0 })
        {
            return configured;
        }

        if (isDevelopment)
        {
            return [];
        }

        throw new InvalidOperationException(
            "cors:allowedOrigins must be configured with at least one origin in non-Development environments.");
    }
}
