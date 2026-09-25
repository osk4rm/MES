using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace AsistOff.MES.Shared.Infrastructure.Correlation;

/// <summary>
/// Reads inbound <c>X-Correlation-ID</c> (generates a GUID when absent or
/// invalid), echoes the effective value on every response, assigns it to
/// <c>HttpContext.TraceIdentifier</c> so Serilog request logging and problem
/// details see the same value, and pushes it into Serilog
/// <c>LogContext</c> so all request logs carry it. Registered first in the
/// Gateway pipeline so error responses are covered too.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationIds.HeaderName].FirstOrDefault();
        var correlationId = CorrelationIds.ResolveIncoming(incoming);

        context.TraceIdentifier = correlationId;
        context.Items[CorrelationIds.ItemKey] = correlationId;

        // Set the echo header before the downstream pipeline runs so it is
        // present even when the global exception handler produces the
        // response; re-apply OnStarting in case headers are cleared.
        context.Response.Headers[CorrelationIds.HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIds.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelationIds.LogPropertyName, correlationId))
        {
            await _next(context);
        }
    }
}
