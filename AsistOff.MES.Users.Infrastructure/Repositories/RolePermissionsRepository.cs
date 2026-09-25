using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Users.Infrastructure.Repositories;

/// <summary>
/// Tenant-filtered role-permission link store. Isolation relies on the global
/// EF query filter; no manual <c>TenantId</c> predicates are used.
/// </summary>
public class RolePermissionsRepository(DefaultContext context) : IRolePermissionsRepository
{
    public Task<RolePermission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.RolePermissions
            .Include(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<RolePermission>> BrowseByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await context.RolePermissions
            .AsNoTracking()
            .Include(x => x.Permission)
            .Where(x => x.RoleId == roleId)
            .OrderBy(x => x.Permission!.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RolePermission>> BrowseAsync(CancellationToken cancellationToken = default)
        => await context.RolePermissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<RolePermission> AddAsync(RolePermission rolePermission, CancellationToken cancellationToken = default)
    {
        await context.RolePermissions.AddAsync(rolePermission, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return rolePermission;
    }
}
