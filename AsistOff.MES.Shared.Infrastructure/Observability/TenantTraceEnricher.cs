using System.Diagnostics;

namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Tags server spans with the ambient tenant id. The tenant id is treated as
/// an opaque tag value only: it is read from the ambient tenant context and
/// can never cross tenant boundaries. An absent tenant (empty GUID) adds no
/// tag.
/// </summary>
public static class TenantTraceEnricher
{
    public const string TenantTagKey = "tenant_id";

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
}
