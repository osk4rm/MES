namespace AsistOff.MES.Multitenancy.Contracts.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantName { get; }
    bool IsTenantActive { get; }
    T? GetTenantClaim<T>(string claimType);
}