using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class LotGenealogyEdgesRepository(DefaultContext context) : ILotGenealogyEdgesRepository
{
    public async Task<IReadOnlyCollection<LotGenealogyEdge>> BrowseAsync(Paginator<LotGenealogyEdge> paginator, CancellationToken cancellationToken = default)
        => await context.Set<LotGenealogyEdge>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<LotGenealogyEdge> predicate, CancellationToken cancellationToken = default)
        => context.Set<LotGenealogyEdge>().Where(predicate).CountAsync(cancellationToken);

    public Task<LotGenealogyEdge?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<LotGenealogyEdge>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<LotGenealogyEdge> AddAsync(LotGenealogyEdge entity, CancellationToken cancellationToken = default)
    {
        context.Set<LotGenealogyEdge>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<LotGenealogyEdge>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LotGenealogyEdge>> ListByProducedLotIdsAsync(IReadOnlyCollection<Guid> producedLotIds, CancellationToken cancellationToken = default)
    {
        if (producedLotIds.Count == 0)
            return Array.Empty<LotGenealogyEdge>();

        return await context.Set<LotGenealogyEdge>()
            .Where(x => producedLotIds.Contains(x.ProducedLotId))
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LotGenealogyEdge>> ListByConsumedLotIdsAsync(IReadOnlyCollection<Guid> consumedLotIds, CancellationToken cancellationToken = default)
    {
        if (consumedLotIds.Count == 0)
            return Array.Empty<LotGenealogyEdge>();

        return await context.Set<LotGenealogyEdge>()
            .Where(x => consumedLotIds.Contains(x.ConsumedLotId))
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
    }
}
