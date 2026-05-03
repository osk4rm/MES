using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.CustomerOrders.Infrastructure.Repositories;

internal sealed class CustomerOrderLinesRepository(DefaultContext context) : ICustomerOrderLinesRepository
{
    public Task<CustomerOrderLine?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<CustomerOrderLine>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<CustomerOrderLine?> GetWithOrderAndReleasesAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<CustomerOrderLine>()
            .Include(x => x.CustomerOrder)
            .ThenInclude(x => x.Customer)
            .Include(x => x.CustomerOrder)
            .ThenInclude(x => x.Lines)
            .Include(x => x.ProductionReleases)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(CustomerOrderLine entity, CancellationToken cancellationToken = default)
    {
        context.Set<CustomerOrderLine>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomerOrderLine entity, CancellationToken cancellationToken = default)
    {
        context.Set<CustomerOrderLine>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<CustomerOrderLine>().Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);

    public async Task AddProductionReleaseAsync(CustomerOrderLineProductionRelease entity, CancellationToken cancellationToken = default)
    {
        context.Set<CustomerOrderLineProductionRelease>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
