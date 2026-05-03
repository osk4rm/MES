using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.CustomerOrders.Infrastructure.Repositories;

internal sealed class CustomerOrdersRepository(DefaultContext context) : ICustomerOrdersRepository
{
    public async Task<IReadOnlyCollection<CustomerOrder>> BrowseAsync(Paginator<CustomerOrder> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<CustomerOrder>()
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Lines)
            .ThenInclude(x => x.ProductionReleases)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(ExpressionStarter<CustomerOrder> predicate, CancellationToken cancellationToken = default)
        => context.Set<CustomerOrder>().Where(predicate).CountAsync(cancellationToken);

    public Task<CustomerOrder?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<CustomerOrder>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<CustomerOrder?> GetWithLinesAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<CustomerOrder>()
            .Include(x => x.Customer)
            .Include(x => x.Lines)
            .ThenInclude(x => x.ProductionReleases)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<CustomerOrder> AddAsync(CustomerOrder entity, CancellationToken cancellationToken = default)
    {
        context.Set<CustomerOrder>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(CustomerOrder entity, CancellationToken cancellationToken = default)
    {
        context.Set<CustomerOrder>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<CustomerOrder>().Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);

    public Task<bool> OrderNumberExistsAsync(string orderNumber, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<CustomerOrder>().AnyAsync(x => x.OrderNumber == orderNumber && (excludeId == null || x.Id != excludeId), cancellationToken);

    public Task<bool> ExternalOrderExistsAsync(string externalSystem, string externalOrderId, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<CustomerOrder>().AnyAsync(x => x.ExternalSystem == externalSystem && x.ExternalOrderId == externalOrderId && (excludeId == null || x.Id != excludeId), cancellationToken);
}
