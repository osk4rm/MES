using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.CustomerOrders.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.CustomerOrders.Infrastructure.Repositories;

internal sealed class CustomersRepository(DefaultContext context) : ICustomersRepository
{
    public Task<IReadOnlyCollection<Customer>> BrowseAsync(Paginator<Customer> paginator, CancellationToken cancellationToken = default)
        => context.Set<Customer>().AsNoTracking().PageFilter(paginator).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyCollection<Customer>)t.Result, cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<Customer> predicate, CancellationToken cancellationToken = default)
        => context.Set<Customer>().Where(predicate).CountAsync(cancellationToken);

    public Task<Customer?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<Customer>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Customer> AddAsync(Customer entity, CancellationToken cancellationToken = default)
    {
        context.Set<Customer>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Customer entity, CancellationToken cancellationToken = default)
    {
        context.Set<Customer>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<Customer>().Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<Customer>().AnyAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId), cancellationToken);
}
