using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Infrastructure.Protection;

/// <summary>
/// Emits a conservative default set of security headers on every API
/// response. Registered first in the Gateway pipeline so headers are present
/// even on error responses produced by the global exception handler.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ContentSecurityPolicyValue =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'";

    public const string ReferrerPolicyValue = "no-referrer";

    public const string StrictTransportSecurityValue = "max-age=31536000; includeSubDomains";

    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // X-Content-Type-Options is unconditional: API responses must never be MIME-sniffed.
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = ReferrerPolicyValue;

        // The Swagger UI (Development only) relies on inline scripts, so the
        // restrictive CSP is skipped there; every real API response carries it.
        if (!IsSwagger(context.Request.Path))
        {
            context.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicyValue;
        }

        // HSTS is only meaningful (and only emitted) over TLS.
        // Issue #369: this check runs after UseForwardedHeaders (see
        // Program.cs pipeline order), so behind the TLS-terminating proxy
        // (Coolify/Traefik -> web nginx -> api over plain HTTP) a request
        // carrying X-Forwarded-Proto: https from a *trusted* proxy already
        // has Scheme rewritten to https here and receives HSTS. A missing or
        // misconfigured TrustedProxies entry fails closed: forwarded headers
        // are ignored (default-deny), Scheme stays http, and no HSTS is
        // emitted — configure TrustedProxies so browsers behind the proxy
        // still get the header. Plain-HTTP health probes and local runs
        // intentionally carry no HSTS.
        if (context.Request.IsHttps)
        {
            context.Response.Headers["Strict-Transport-Security"] = StrictTransportSecurityValue;
        }

        await _next(context);
    }

    internal static bool IsSwagger(PathString path)
        => path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
}
