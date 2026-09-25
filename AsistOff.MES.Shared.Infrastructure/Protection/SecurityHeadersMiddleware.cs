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
        if (context.Request.IsHttps)
        {
            context.Response.Headers["Strict-Transport-Security"] = StrictTransportSecurityValue;
        }

        await _next(context);
    }

    internal static bool IsSwagger(PathString path)
        => path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
}
