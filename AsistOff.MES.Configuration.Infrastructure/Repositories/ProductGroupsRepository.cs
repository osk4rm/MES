using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class ProductGroupsRepository(DefaultContext context) : IProductGroupsRepository
{
    public async Task<IReadOnlyCollection<ProductGroup>> BrowseAsync(Paginator<ProductGroup> paginator, CancellationToken cancellationToken = default)
    {
        var units = await context.ProductGroups
            .Include(x => x.ParentGroup)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

        return units;
    }

    public Task<ProductGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return context.ProductGroups
            .Include(x => x.ParentGroup)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<int> CountAsync(ExpressionStarter<ProductGroup> predicate, CancellationToken cancellationToken = default)
    {
        return await context.ProductGroups
            .Where(predicate)
            .CountAsync(cancellationToken);
    }

    public async Task<ProductGroup> AddAsync(ProductGroup entity, CancellationToken cancellationToken = default)
    {
        context.ProductGroups.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ProductGroup entity, CancellationToken cancellationToken = default)
    {
        context.ProductGroups.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.ProductGroups
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}