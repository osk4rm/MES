using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class WarehousesRepository(DefaultContext context) : IWarehousesRepository
{
    public async Task<IReadOnlyCollection<Warehouse>> BrowseAsync(CancellationToken cancellationToken = default)
    {
        return await context.Warehouses.ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<Warehouse?> GetByIdAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.FindAsync<Warehouse>([id], cancellationToken);
    }

    public async Task<Warehouse> AddAsync(Warehouse warehouse,
        CancellationToken cancellationToken = default)
    {
        context.Add(warehouse);
        await context.SaveChangesAsync(cancellationToken);

        return warehouse;
    }

    public async Task UpdateAsync(Warehouse warehouse,
        CancellationToken cancellationToken = default)
    {
        context.Update(warehouse);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await GetByIdAsync(id, cancellationToken);

        context.Remove(warehouse);
        await context.SaveChangesAsync(cancellationToken);
    }
}