using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Users.Infrastructure.Repositories;

/// <summary>
/// Tenant-filtered permission store. Isolation relies on the global EF query filter;
/// no manual <c>TenantId</c> predicates are used.
/// </summary>
public class PermissionsRepository(DefaultContext context) : IPermissionsRepository
{
    public Task<Permission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Permissions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Permission?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => context.Permissions.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);

    public async Task<IReadOnlyCollection<Permission>> BrowseAsync(CancellationToken cancellationToken = default)
        => await context.Permissions
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

    public async Task<Permission> AddAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        await context.Permissions.AddAsync(permission, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return permission;
    }
}
