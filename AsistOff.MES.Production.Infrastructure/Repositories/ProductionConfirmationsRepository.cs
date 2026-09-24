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
}
