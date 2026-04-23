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
            var value = _httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value
                ?? throw new UnauthorizedAccessException("Tenant not found");

            if (!Guid.TryParse(value, out var tenantId))
            {
                throw new UnauthorizedAccessException("Tenant claim contains an invalid identifier.");
            }

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
        tenantId = Guid.Empty;
        var value = _httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Guid.TryParse(value, out tenantId) && tenantId != Guid.Empty;
    }
}