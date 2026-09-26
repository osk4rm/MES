using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Infrastructure.Outbox;

namespace AsistOff.MES.Multitenancy;

/// <summary>
/// Slice 2 (#259) <see cref="IOutboxTenantSource"/> backed by the tenant
/// registry. The outbox relay iterates exactly the active tenants so each
/// tenant's staged rows are dispatched under that tenant's scope.
/// </summary>
public sealed class OutboxTenantSource(ITenantRepository tenantRepository) : IOutboxTenantSource
{
    public Task<IReadOnlyCollection<Guid>> ListActiveTenantIdsAsync(CancellationToken cancellationToken = default)
        => tenantRepository.ListActiveIdsAsync(cancellationToken);
}
