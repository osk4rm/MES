using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Resolves the effective <c>X-Correlation-ID</c> for every request (bridge
/// for issue #251): echoes the value on the response, stores it in
/// <c>HttpContext.Items</c> for the error envelopes, pushes it into the
/// Serilog <c>LogContext</c> so all request logs carry it, and attaches it
/// to the active trace as baggage plus a span tag.
/// Runs before tenant resolution and never reads any tenant state, so it
/// cannot cross tenant boundaries.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var effective = CorrelationIdHelper.ResolveCorrelationId(
            context.Request.Headers[CorrelationIdHelper.HeaderName].FirstOrDefault());

        context.Items[CorrelationIdHelper.ItemKey] = effective;

        // Set eagerly so the value is visible in-memory, and re-apply in
        // OnStarting so it survives a downstream Response.Clear() (e.g. the
        // global exception handler) on error paths.
        context.Response.Headers[CorrelationIdHelper.HeaderName] = effective;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHelper.HeaderName] = effective;
            return Task.CompletedTask;
        });

        CorrelationIdHelper.AttachToTrace(effective);

        using (LogContext.PushProperty("CorrelationId", effective))
        {
            await next(context);
        }
    }
}
