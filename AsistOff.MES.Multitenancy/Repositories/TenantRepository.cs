using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Entity;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Multitenancy.Repositories;

internal sealed class TenantRepository(MultitenancyDbContext db) : ITenantRepository
{
    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task CreateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
    }
}

