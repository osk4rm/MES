using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;

namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Pure helper for the <c>X-Correlation-ID</c> contract (shared with issue
/// #251): a valid incoming GUID is echoed verbatim, anything absent or
/// invalid is replaced with a freshly generated GUID. The effective value is
/// attached to the active W3C trace (span tag + baggage) so logs, traces and
/// error envelopes can be joined on a single identifier.
/// </summary>
public static class CorrelationIdHelper
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";
    public const string BaggageKey = "correlation-id";
    public const string ActivityTagKey = "correlation.id";

    /// <summary>
    /// Whether the value is a usable correlation id (a parseable GUID).
    /// </summary>
    public static bool IsValidCorrelationId(string? value) => Guid.TryParse(value, out _);

    /// <summary>
    /// Keeps a valid incoming id, otherwise generates a fresh GUID string.
    /// </summary>
    public static string ResolveCorrelationId(string? incoming) =>
        IsValidCorrelationId(incoming) ? incoming! : GenerateCorrelationId();

    /// <summary>
    /// Generates a fresh correlation id.
    /// </summary>
    public static string GenerateCorrelationId() => Guid.NewGuid().ToString();

    /// <summary>
    /// Attaches the effective correlation id to the active trace: a
    /// <c>correlation.id</c> tag on the current <see cref="Activity"/> (when
    /// present) and a <c>correlation-id</c> baggage entry so the value
    /// propagates to downstream services via the W3C <c>baggage</c> header.
    /// Never throws when no activity is active.
    /// </summary>
    public static void AttachToTrace(string correlationId)
    {
        Activity.Current?.SetTag(ActivityTagKey, correlationId);
        Activity.Current?.AddBaggage(BaggageKey, correlationId);
        Baggage.Current = Baggage.Current.SetBaggage(BaggageKey, correlationId);
    }

    /// <summary>
    /// Reads the effective correlation id stored by
    /// <see cref="CorrelationIdMiddleware"/> in <c>HttpContext.Items</c>.
    /// Returns <c>null</c> when the middleware has not run or the stored
    /// value is not a valid id.
    /// </summary>
    public static string? GetEffectiveCorrelationId(HttpContext? context)
    {
        if (context?.Items.TryGetValue(ItemKey, out var value) is true
            && value is string candidate
            && IsValidCorrelationId(candidate))
        {
            return candidate;
        }

        return null;
    }
}
