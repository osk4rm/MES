using System.Diagnostics;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Correlation;

namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Bridges the effective <c>X-Correlation-ID</c> (issue #251) into the active
/// W3C trace and enriches server spans with the ambient tenant id (issue #252).
/// The correlation value stays an opaque GUID; tenant_id is read from the
/// ambient <see cref="ICurrentTenantAccessor"/> as a span tag only and can
/// never cross tenant boundaries. No new entities, no query-filter bypass.
/// </summary>
public static class ObservabilityPropagation
{
    /// <summary>Baggage key carrying the effective correlation ID.</summary>
    public const string CorrelationBaggageKey = "correlation-id";

    /// <summary>Span tag carrying the effective correlation ID.</summary>
    public const string CorrelationTagKey = "correlation.id";

    /// <summary>Span tag carrying the ambient tenant id.</summary>
    public const string TenantTagKey = "tenant_id";

    /// <summary>
    /// Resolves the effective correlation ID for an incoming header value:
    /// a parseable GUID is kept verbatim (trimmed), anything else (absent,
    /// empty, invalid) is replaced with a freshly generated GUID.
    /// Delegates to <see cref="CorrelationIds.ResolveIncoming"/> so both
    /// issues share identical semantics.
    /// </summary>
    public static string ResolveCorrelationId(string? incoming)
        => CorrelationIds.ResolveIncoming(incoming);

    /// <summary>
    /// Attaches the effective correlation ID to the given activity as baggage
    /// (propagated to downstream HttpClient/EF spans) and as a tag (exported
    /// on the span itself). No-op when the activity is null or the value is
    /// blank. Returns <c>true</c> when the activity was enriched.
    /// </summary>
    public static bool AttachCorrelation(Activity? activity, string? correlationId)
    {
        if (activity is null || string.IsNullOrWhiteSpace(correlationId))
        {
            return false;
        }

        activity.AddBaggage(CorrelationBaggageKey, correlationId);
        activity.SetTag(CorrelationTagKey, correlationId);
        return true;
    }

    /// <summary>
    /// Attaches the ambient tenant id to the given activity as a
    /// <c>tenant_id</c> tag. Adds no tag (returns <c>false</c>) when the
    /// activity is null, the accessor is null, or no tenant is available.
    /// </summary>
    public static bool TryAttachTenant(Activity? activity, ICurrentTenantAccessor? accessor)
    {
        if (activity is null || accessor is null)
        {
            return false;
        }

        if (!accessor.TryGetTenantId(out var tenantId) || tenantId == Guid.Empty)
        {
            return false;
        }

        activity.SetTag(TenantTagKey, tenantId.ToString());
        return true;
    }
}
