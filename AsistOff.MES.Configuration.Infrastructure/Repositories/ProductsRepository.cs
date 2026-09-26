using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class ProductsRepository(DefaultContext context) : IProductsRepository
{
    public async Task<IReadOnlyCollection<Product>> BrowseAsync(Paginator<Product> paginator, CancellationToken cancellationToken = default)
    {
        var products = await context.Products
            .AsNoTracking()
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

    public async Task<Product?> GetByScanAsync(string value, CancellationToken cancellationToken = default)
    {
        // Resolution priority: exact Code, then Ean, then Barcode — active products only.
        // Tenant isolation is enforced by the EF Core global query filter (ISaasy).
        var byCode = await context.Products
            .Include(x => x.ProductGroup)
            .Include(x => x.ProductMeasureUnits)
                .ThenInclude(pmu => pmu.MeasureUnit)
            .FirstOrDefaultAsync(x => x.IsActive && x.Code == value, cancellationToken);

        if (byCode is not null)
            return byCode;

        var byEan = await context.Products
            .Include(x => x.ProductGroup)
            .Include(x => x.ProductMeasureUnits)
                .ThenInclude(pmu => pmu.MeasureUnit)
            .FirstOrDefaultAsync(x => x.IsActive && x.Ean == value, cancellationToken);

        if (byEan is not null)
            return byEan;

        return await context.Products
            .Include(x => x.ProductGroup)
            .Include(x => x.ProductMeasureUnits)
                .ThenInclude(pmu => pmu.MeasureUnit)
            .FirstOrDefaultAsync(x => x.IsActive && x.Barcode == value, cancellationToken);
    }

    public async Task<bool> EanExistsAsync(string ean, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        // Tenant isolation is enforced by the EF Core global query filter (ISaasy):
        // the same Ean may exist in another tenant without conflicting.
        var query = context.Products.Where(x => x.Ean == ean);

        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> CountAsync(ExpressionStarter<Product> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Products.Where(predicate).CountAsync(cancellationToken);
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