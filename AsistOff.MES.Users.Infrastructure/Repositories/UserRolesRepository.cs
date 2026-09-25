using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Users.Infrastructure.Repositories;

/// <summary>
/// Tenant-filtered user-role link store. Isolation relies on the global
/// EF query filter; no manual <c>TenantId</c> predicates are used.
/// </summary>
public class UserRolesRepository(DefaultContext context) : IUserRolesRepository
{
    public Task<UserRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.UserRoles
            .Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<UserRole>> BrowseByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.UserRoles
            .AsNoTracking()
            .Include(x => x.Role)
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<UserRole>> BrowseByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await context.UserRoles
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.RoleId == roleId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<UserRole>> BrowseAsync(CancellationToken cancellationToken = default)
        => await context.UserRoles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<UserRole> AddAsync(UserRole userRole, CancellationToken cancellationToken = default)
    {
        await context.UserRoles.AddAsync(userRole, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return userRole;
    }

    public async Task RemoveAsync(UserRole userRole, CancellationToken cancellationToken = default)
    {
        // The link instance typically arrives detached (AsNoTracking browse with an
        // Include) while the caller holds the same Role tracked (GetByIdAsync).
        // Remove() would attach the whole reachable graph and clash on the duplicate
        // Role instance ("another instance with the same key ... is already being
        // tracked" -> HTTP 500). Marking only this entry Deleted attaches just the
        // link row, which is all a delete needs.
        context.Entry(userRole).State = EntityState.Deleted;
        await context.SaveChangesAsync(cancellationToken);
    }
}
