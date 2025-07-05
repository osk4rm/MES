using AsistOff.MES.Multitenancy.Entity;

namespace AsistOff.MES.Multitenancy.Interfaces;

public interface ISaasy
{
    Guid TenantId { get; }
    Tenant Tenant { get; }
}