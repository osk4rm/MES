using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Tags server spans with the ambient tenant id. The tenant id is treated as
/// an opaque tag value only: it is read from the ambient tenant context and
/// can never cross tenant boundaries. An absent tenant (empty GUID) adds no
/// tag.
///
/// The id travels on <c>HttpContext.Items</c> (stashed by
/// <c>TenantTraceContextMiddleware</c> while the request scope is alive):
/// the OpenTelemetry response-enrichment callback runs after ASP.NET Core has
/// already torn down <c>HttpContext.RequestServices</c>, so resolving
/// services there throws and is swallowed by the SDK. Items survive until
/// export and are the only safe channel.
/// </summary>
public static class TenantTraceEnricher
{
    public const string TenantTagKey = "tenant_id";

    /// <summary>
    /// <c>HttpContext.Items</c> key carrying the resolved tenant id for the
    /// current request.
    /// </summary>
    public const string TenantItemKey = "TenantId";

    /// <summary>
    /// Sets the <c>tenant_id</c> span tag. Returns <c>false</c> and adds no
    /// tag when the activity is <c>null</c> or no tenant is available
    /// (<see cref="Guid.Empty"/>).
    /// </summary>
    public static bool TryEnrichWithTenant(Activity? activity, Guid tenantId)
    {
        if (activity is null || tenantId == Guid.Empty)
        {
            return false;
        }

        activity.SetTag(TenantTagKey, tenantId.ToString());
        return true;
    }

    /// <summary>
    /// Reads the stashed tenant id back from <c>HttpContext.Items</c>.
    /// Returns <c>false</c> when no usable id was stashed (anonymous
    /// infrastructure endpoints, unresolved tenant).
    /// </summary>
    public static bool TryReadTenantId(HttpContext? context, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var stored = context?.Items.TryGetValue(TenantItemKey, out var value) == true ? value : null;
        return stored switch
        {
            Guid id when id != Guid.Empty => (tenantId = id) != Guid.Empty,
            string s when Guid.TryParse(s, out var parsed) && parsed != Guid.Empty => (tenantId = parsed) != Guid.Empty,
            _ => false,
        };
    }
}
