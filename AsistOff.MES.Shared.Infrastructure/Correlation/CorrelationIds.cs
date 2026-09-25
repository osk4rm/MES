using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;

namespace AsistOff.MES.Shared.Infrastructure.Correlation;

/// <summary>
/// Transports an opaque per-request correlation ID end-to-end (issue #251).
/// The middleware runs before tenant resolution and never reads
/// <c>TenantId</c>; the value is an opaque GUID and cannot cross tenant
/// boundaries. No new tenant-scoped entities and no query-filter bypass.
/// </summary>
public static class CorrelationIds
{
    public const string HeaderName = "X-Correlation-ID";

    public const string ItemKey = "CorrelationId";

    public const string LogPropertyName = "CorrelationId";

    /// <summary>
    /// W3C baggage key carrying the effective correlation ID downstream.
    /// </summary>
    public const string BaggageKey = "correlation-id";

    /// <summary>
    /// Span tag key carrying the effective correlation ID on the server span.
    /// </summary>
    public const string ActivityTagKey = "correlation.id";

    /// <summary>
    /// Resolves the effective correlation ID for an incoming header value:
    /// a parseable GUID is echoed back verbatim (trimmed), anything else
    /// (absent, empty, invalid) is replaced with a freshly generated GUID.
    /// </summary>
    public static string ResolveIncoming(string? incoming)
    {
        var candidate = incoming?.Trim();

        if (!string.IsNullOrEmpty(candidate) && Guid.TryParse(candidate, out _))
        {
            return candidate;
        }

        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Returns the effective correlation ID for the current request: the
    /// value stored by <c>CorrelationIdMiddleware</c>, falling back to
    /// <c>TraceIdentifier</c> when it already holds a GUID (the middleware
    /// assigns <c>TraceIdentifier</c> to the correlation ID), otherwise null.
    /// </summary>
    public static string? GetCurrent(HttpContext? context)
    {
        if (context is null)
        {
            return null;
        }

        if (context.Items.TryGetValue(ItemKey, out var stored) && stored is string s && Guid.TryParse(s, out _))
        {
            return s;
        }

        return Guid.TryParse(context.TraceIdentifier, out _) ? context.TraceIdentifier : null;
    }

    /// <summary>
    /// Attaches the effective correlation ID to the active W3C trace
    /// (issue #252): a <c>correlation.id</c> tag on the current
    /// <see cref="Activity"/> (when present) and a <c>correlation-id</c>
    /// baggage entry so the value propagates to downstream services via the
    /// W3C <c>baggage</c> header. Never throws when no activity is active.
    /// </summary>
    public static void AttachToTrace(string correlationId)
    {
        Activity.Current?.SetTag(ActivityTagKey, correlationId);
        Activity.Current?.AddBaggage(BaggageKey, correlationId);
        Baggage.Current = Baggage.Current.SetBaggage(BaggageKey, correlationId);
    }
}
