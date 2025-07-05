namespace AsistOff.MES.Multitenancy.Context;

public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantName { get; }
    bool IsTenantActive { get; }
    T? GetTenantClaim<T>(string claimType);
}