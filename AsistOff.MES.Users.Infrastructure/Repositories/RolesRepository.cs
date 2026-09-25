using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Users.Infrastructure.Repositories;

/// <summary>
/// Tenant-filtered role store. Isolation relies on the global EF query filter;
/// no manual <c>TenantId</c> predicates are used.
/// </summary>
public class RolesRepository(DefaultContext context) : IRolesRepository
{
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Roles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => context.Roles.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);

    public async Task<IReadOnlyCollection<Role>> BrowseAsync(CancellationToken cancellationToken = default)
        => await context.Roles
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

    public async Task<Role> AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        await context.Roles.AddAsync(role, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        context.Roles.Update(role);
        await context.SaveChangesAsync(cancellationToken);
    }
}
