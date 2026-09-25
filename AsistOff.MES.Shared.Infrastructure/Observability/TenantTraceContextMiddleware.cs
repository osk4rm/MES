using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Stashes the resolved tenant id on <c>HttpContext.Items</c> (issue #252)
/// so the OpenTelemetry response-enrichment callback can tag the server span
/// with <c>tenant_id</c>. The stash happens after the downstream pipeline
/// completes: tenant resolution (JWT claims via authentication) has finished
/// and the request scope is still alive. Reading it later from
/// <c>RequestServices</c> is impossible — ASP.NET Core tears the scope down
/// before the OTel stop event fires, and any throw there is swallowed by the
/// SDK. Reads tenant state as an opaque value only; never touches tenant
/// data and performs no query-filter bypass.
/// </summary>
public sealed class TenantTraceContextMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        var accessor = context.RequestServices.GetService<ICurrentTenantAccessor>();
        if (accessor is not null && accessor.TryGetTenantId(out var tenantId) && tenantId != Guid.Empty)
        {
            context.Items[TenantTraceEnricher.TenantItemKey] = tenantId;
        }
    }
}
