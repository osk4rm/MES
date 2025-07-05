using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Multitenancy.Context;

internal sealed class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId =>
        Guid.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value ??
                   throw new UnauthorizedAccessException("Tenant not found"));

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
}