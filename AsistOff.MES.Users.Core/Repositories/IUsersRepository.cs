using AsistOff.MES.Users.Core.Entities;

namespace AsistOff.MES.Users.Core.Repositories;

public interface IUsersRepository
{
    Task<User?> GetAsync(Guid id);
    Task<User?> GetAsync(string email);

    /// <summary>
    /// Look up a user by email bypassing the tenant query filter.
    /// Use ONLY for pre‑authentication flows (sign‑in) where the ambient
    /// tenant is not yet established. All other flows must use <see cref="GetAsync(string)"/>.
    /// </summary>
    Task<User?> GetForAuthenticationAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Look up a user by email and tenant ID bypassing the global tenant query filter.
    /// Use ONLY for idempotency checks in anonymous/system flows (e.g. event listeners)
    /// where no ambient tenant is established. Scopes the check to the given tenant,
    /// so it does not act as a cross-tenant uniqueness guard.
    /// </summary>
    Task<User?> GetByEmailAndTenantIgnoringQueryFiltersAsync(string email, Guid tenantId, CancellationToken cancellationToken = default);

    Task AddAsync(User user);
    Task UpdateAsync(User user);
}