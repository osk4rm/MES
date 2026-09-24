using AsistOff.MES.Multitenancy.Entity;

namespace AsistOff.MES.Multitenancy.Repositories;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task CreateAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids of all active tenants. Used by background workers (e.g. the
    /// telemetry simulator) to iterate tenants one scope at a time.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> ListActiveIdsAsync(CancellationToken cancellationToken = default);
}

