using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AsistOff.MES.Shared.Infrastructure.Health;

/// <summary>
/// Response writer for the Gateway health probes. Healthy reports return
/// <c>200 application/json</c>; anything else returns <c>503
/// application/problem+json</c> (RFC 7807) so orchestrators and existing
/// callers get a machine-readable failure.
///
/// Only the aggregate status and the probe path are serialized — per-check
/// descriptions and exceptions are never written, so provider error details
/// (hosts, usernames, connection keywords) cannot leak into the body.
/// </summary>
public static class HealthProbeResponseWriter
{
    public static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        if (report.Status == HealthStatus.Healthy)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status200OK;
            return context.Response.WriteAsync("""{"status":"Healthy"}""");
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        var payload = JsonSerializer.Serialize(new
        {
            type = "https://httpstatuses.com/503",
            title = "Service Unavailable",
            status = StatusCodes.Status503ServiceUnavailable,
            detail = "Readiness probe failed: the service is temporarily unable to serve traffic.",
            instance = context.Request.Path.Value
        });

        return context.Response.WriteAsync(payload);
    }
}
