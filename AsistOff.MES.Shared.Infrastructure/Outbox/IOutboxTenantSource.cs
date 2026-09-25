namespace AsistOff.MES.Shared.Infrastructure.Outbox;

/// <summary>
/// Slice 2 (#259) source of tenant ids for the outbox relay. The relay must
/// iterate tenants one scope at a time so the EF Core global query filter
/// isolates each tenant's outbox rows with no <c>IgnoreQueryFilters</c>
/// bypass — but <c>Shared.Infrastructure</c> cannot reference the
/// Multitenancy module, so the module implements this contract (backed by
/// <c>ITenantRepository.ListActiveIdsAsync</c>) following the same
/// define-here/implement-in-module pattern as <c>IEntityConfigurator</c>.
/// </summary>
public interface IOutboxTenantSource
{
    /// <summary>Ids of all active tenants to relay outbox rows for.</summary>
    Task<IReadOnlyCollection<Guid>> ListActiveTenantIdsAsync(CancellationToken cancellationToken = default);
}
