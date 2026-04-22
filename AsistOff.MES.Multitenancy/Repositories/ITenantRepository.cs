using AsistOff.MES.Multitenancy.Entity;

namespace AsistOff.MES.Multitenancy.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task CreateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}

