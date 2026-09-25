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
    /// Resolves the per-client partition key: the left-most
    /// <c>X-Forwarded-For</c> entry when present (standard deployment behind a
    /// reverse proxy), otherwise the connection remote address, otherwise a
    /// constant fallback. Deployments directly exposed to the internet should
    /// strip/spoof-proof <c>X-Forwarded-For</c> at the edge, otherwise a
    /// caller can rotate the header to escape the throttle.
    /// </summary>
    public static string ResolveClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var first = forwarded.ToString().Split(',').Select(part => part.Trim()).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>Full limiter partition key: <c>{scope}:{client-ip}</c>.</summary>
    public static string BuildPartitionKey(HttpContext context, string scope)
        => $"{scope}:{ResolveClientIp(context)}";
}
