using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class ProductionOrdersRepository(DefaultContext context) : IProductionOrdersRepository
{
    public async Task<IReadOnlyCollection<ProductionOrder>> BrowseAsync(Paginator<ProductionOrder> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<ProductionOrder>()
            .AsNoTracking()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(ExpressionStarter<ProductionOrder> predicate, CancellationToken cancellationToken = default)
        => context.Set<ProductionOrder>().Where(predicate).CountAsync(cancellationToken);

    public Task<ProductionOrder?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<ProductionOrder>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<ProductionOrder> AddAsync(ProductionOrder entity, CancellationToken cancellationToken = default)
    {
        context.Set<ProductionOrder>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ProductionOrder entity, CancellationToken cancellationToken = default)
    {
        context.Set<ProductionOrder>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<ProductionOrder>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<ProductionOrder>()
            .AnyAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId), cancellationToken);
}
