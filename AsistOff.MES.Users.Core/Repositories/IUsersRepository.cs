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

    Task AddAsync(User user);
    Task UpdateAsync(User user);
}