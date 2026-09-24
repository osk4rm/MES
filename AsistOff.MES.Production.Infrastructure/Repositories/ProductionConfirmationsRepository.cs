using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class ProductionConfirmationsRepository(DefaultContext context) : IProductionConfirmationsRepository
{
    public async Task<IReadOnlyCollection<ProductionConfirmation>> BrowseAsync(Paginator<ProductionConfirmation> paginator, CancellationToken cancellationToken = default)
        => await context.Set<ProductionConfirmation>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<ProductionConfirmation> predicate, CancellationToken cancellationToken = default)
        => context.Set<ProductionConfirmation>().Where(predicate).CountAsync(cancellationToken);

    public Task<ProductionConfirmation?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<ProductionConfirmation>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<ProductionConfirmation> AddAsync(ProductionConfirmation entity, CancellationToken cancellationToken = default)
    {
        context.Set<ProductionConfirmation>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<ProductionConfirmation>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<(decimal ProducedQuantity, decimal ScrappedQuantity, int ConfirmationsCount)> GetTotalsAsync(
        Guid productionOrderId, CancellationToken cancellationToken = default)
    {
        var totals = await GetTotalsForOrdersAsync([productionOrderId], cancellationToken);
        return totals.TryGetValue(productionOrderId, out var total)
            ? total
            : (0m, 0m, 0);
    }

    public async Task<Dictionary<Guid, (decimal ProducedQuantity, decimal ScrappedQuantity, int ConfirmationsCount)>> GetTotalsForOrdersAsync(
        IReadOnlyCollection<Guid> productionOrderIds, CancellationToken cancellationToken = default)
    {
        if (productionOrderIds.Count == 0)
            return new Dictionary<Guid, (decimal, decimal, int)>();

        return await context.Set<ProductionConfirmation>()
            .AsNoTracking()
            .Where(x => productionOrderIds.Contains(x.ProductionOrderId))
            .GroupBy(x => x.ProductionOrderId)
            .Select(g => new
            {
                ProductionOrderId = g.Key,
                ProducedQuantity = g.Sum(x => x.GoodQuantity),
                ScrappedQuantity = g.Sum(x => x.ScrapQuantity),
                ConfirmationsCount = g.Count()
            })
            .ToDictionaryAsync(
                x => x.ProductionOrderId,
                x => (x.ProducedQuantity, x.ScrappedQuantity, x.ConfirmationsCount),
                cancellationToken);
    }
}
