using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class ProductsRepository(DefaultContext context) : IProductsRepository
{
    public async Task<IReadOnlyCollection<Product>> BrowseAsync(Paginator<Product> paginator, CancellationToken cancellationToken = default)
    {
        var products = await context.Products
            .Include(x => x.ProductGroup)
            .Include(x => x.ProductMeasureUnits)
                .ThenInclude(pmu => pmu.MeasureUnit)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

        return products;
    }

    public Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return context.Products
            .Include(x => x.ProductGroup)
            .Include(x => x.ProductMeasureUnits)
                .ThenInclude(pmu => pmu.MeasureUnit)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await context.Products.CountAsync(cancellationToken);
    }

    public async Task<Product> AddAsync(Product entity, CancellationToken cancellationToken = default)
    {
        context.Products.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        context.Products.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Products
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}