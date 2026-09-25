using AsistOff.MES.Users.Core.Entities;

namespace AsistOff.MES.Users.Core.Repositories;

/// <summary>
/// Tenant-filtered refresh-token store. Isolation relies on the global EF
/// query filter; no manual <c>TenantId</c> predicates are used.
/// </summary>
public interface IRefreshTokensRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RefreshToken>> BrowseActiveByUserAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RefreshToken>> BrowseByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
