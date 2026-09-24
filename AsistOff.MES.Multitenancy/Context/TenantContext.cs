using System.Text.Json;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Multitenancy.Context;

internal sealed class TenantContext : ITenantContext, ICurrentTenantAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            if (!TryGetTenantId(out var tenantId))
                throw new UnauthorizedAccessException("Tenant not found");

            return tenantId;
        }
    }

    public string TenantName =>
        _httpContextAccessor.HttpContext?.User.FindFirst("tenant_name")?.Value ??
        throw new UnauthorizedAccessException("Tenant not found");

    public bool IsTenantActive =>
        bool.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("tenant_active")?.Value ?? "false");

    public T? GetTenantClaim<T>(string claimType)
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
        return claim != null ? JsonSerializer.Deserialize<T>(claim) : default;
    }

    public Guid CurrentTenantId => TryGetTenantId(out var tenantId) ? tenantId : Guid.Empty;

    public bool TryGetTenantId(out Guid tenantId)
    {
        // Background (non-HTTP) work runs inside an explicit
        // BackgroundTenantContext scope; it wins over request claims so that
        // hosted services resolve exactly one tenant at a time.
        var background = BackgroundTenantContext.Current;
        if (background.HasValue && background.Value != Guid.Empty)
        {
            tenantId = background.Value;
            return true;
        }

        tenantId = Guid.Empty;
        var value = _httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Guid.TryParse(value, out tenantId) && tenantId != Guid.Empty;
    }
}