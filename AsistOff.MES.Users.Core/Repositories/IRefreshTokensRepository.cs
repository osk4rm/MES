using AsistOff.MES.Users.Core.Entities;

namespace AsistOff.MES.Users.Core.Repositories;

/// <summary>
/// Tenant-filtered refresh-token store. Isolation relies on the global EF
/// query filter; no manual <c>TenantId</c> predicates are used.
/// </summary>
public interface IRefreshTokensRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    /// <summary>
    /// Pre-authentication lookup for the anonymous refresh endpoint: resolves the
    /// stored row by token hash before any tenant is known, so the global tenant
    /// query filter is bypassed here (same justification as
    /// <c>IUsersRepository.GetForAuthenticationAsync</c>). Tenant binding comes
    /// from the returned row's <c>TenantId</c> — never from caller input — and all
    /// subsequent reads/writes run inside that tenant's scope.
    /// </summary>
    Task<RefreshToken?> GetByHashIgnoringQueryFiltersAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RefreshToken>> BrowseActiveByUserAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RefreshToken>> BrowseByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
