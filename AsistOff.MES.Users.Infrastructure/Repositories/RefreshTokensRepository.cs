using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Users.Infrastructure.Repositories;

/// <summary>
/// Tenant-filtered refresh-token store. Isolation relies on the global
/// EF query filter; no manual <c>TenantId</c> predicates are used.
/// </summary>
public class RefreshTokensRepository(DefaultContext context) : IRefreshTokensRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => context.Set<RefreshToken>()
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    /// <summary>
    /// Pre-authentication lookup for the anonymous refresh endpoint. Bypasses the
    /// global tenant query filter because no ambient tenant exists yet (the caller
    /// presents only an opaque token, potentially with an expired access token).
    /// Justified like <c>UsersRepository.GetForAuthenticationAsync</c>: the opaque
    /// hash is unguessable (256-bit entropy) and tenant binding is enforced from
    /// the returned row's <c>TenantId</c> by entering that tenant's scope before
    /// any further read or write.
    /// </summary>
    public Task<RefreshToken?> GetByHashIgnoringQueryFiltersAsync(string tokenHash, CancellationToken cancellationToken = default)
        => context.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyCollection<RefreshToken>> BrowseActiveByUserAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default)
        => await context.Set<RefreshToken>()
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RefreshToken>> BrowseByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default)
        => await context.Set<RefreshToken>()
            .Where(x => x.FamilyId == familyId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await context.Set<RefreshToken>().AddAsync(refreshToken, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        context.Set<RefreshToken>().Update(refreshToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
