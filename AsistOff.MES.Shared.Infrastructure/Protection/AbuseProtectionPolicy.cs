using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Infrastructure.Protection;

/// <summary>
/// Pure endpoint-matching and client-identity logic for the abuse-protection
/// rate limiter. Kept free of rate-limiter framework types so it stays unit
/// testable; the Gateway wires it into <c>AddRateLimiter</c> via
/// <c>AbuseProtectionRegistration</c>.
///
/// Only the two anonymous bootstrap endpoints are throttled — every other
/// request (including authenticated reads) bypasses the limiter entirely.
/// </summary>
public static class AbuseProtectionPolicy
{
    public const string SignInScope = "signin";

    public const string TenantCreateScope = "tenant-signup";

    private const string SignInPath = "/api/auth/sign-in";

    private const string TenantCreatePath = "/api/tenants";

    /// <summary>
    /// Returns the throttle scope for anonymous bootstrap attempts, or
    /// <c>null</c> when the request must bypass the limiter (any non-POST,
    /// any other path, including <c>GET /api/tenants/{id}</c>).
    /// </summary>
    public static string? MatchScope(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return null;
        }

        if (request.Path.Equals(SignInPath, StringComparison.OrdinalIgnoreCase))
        {
            return SignInScope;
        }

        if (request.Path.Equals(TenantCreatePath, StringComparison.OrdinalIgnoreCase))
        {
            return TenantCreateScope;
        }

        return null;
    }

    /// <summary>
    /// Resolves the per-client partition key from the TCP source address
    /// (<c>Connection.RemoteIpAddress</c>) only. Forwarded headers
    /// (<c>X-Forwarded-For</c>) are deliberately never read here: any caller
    /// could rotate the header value and escape the throttle entirely. Behind
    /// a reverse proxy the ASP.NET Core Forwarded Headers middleware (wired in
    /// <c>Program.cs</c> with config-bound <see cref="TrustedProxyOptions"/>,
    /// default deny) already rewrote <c>RemoteIpAddress</c> to the real client
    /// IP when — and only when — the immediate peer is an explicitly trusted
    /// proxy/network, so reading <c>RemoteIpAddress</c> honors the nginx
    /// <c>proxy_set_header X-Forwarded-For</c> deployment without trusting
    /// spoofable input on direct connections. Falls back to
    /// <c>"unknown"</c> when the transport exposes no address.
    /// </summary>
    public static string ResolveClientIp(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>Full limiter partition key: <c>{scope}:{client-ip}</c>.</summary>
    public static string BuildPartitionKey(HttpContext context, string scope)
        => $"{scope}:{ResolveClientIp(context)}";
}
