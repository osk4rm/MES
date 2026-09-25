using Microsoft.AspNetCore.Http;

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
}
