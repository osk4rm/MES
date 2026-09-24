using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class KanbanCardsRepository(DefaultContext context) : IKanbanCardsRepository
{
    public async Task<IReadOnlyCollection<KanbanCard>> BrowseAsync(Paginator<KanbanCard> paginator, CancellationToken cancellationToken = default)
        => await context.Set<KanbanCard>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<KanbanCard> predicate, CancellationToken cancellationToken = default)
        => context.Set<KanbanCard>().Where(predicate).CountAsync(cancellationToken);

    public Task<KanbanCard?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<KanbanCard>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<KanbanCard> AddAsync(KanbanCard entity, CancellationToken cancellationToken = default)
    {
        context.Set<KanbanCard>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(KanbanCard entity, CancellationToken cancellationToken = default)
    {
        context.Set<KanbanCard>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<KanbanCard>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> CardNumberExistsAsync(Guid loopId, string cardNumber, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<KanbanCard>()
            .AnyAsync(x => x.LoopId == loopId && x.CardNumber == cardNumber && (excludeId == null || x.Id != excludeId), cancellationToken);

    public Task<int> CountByLoopAsync(Guid loopId, CancellationToken cancellationToken = default)
        => context.Set<KanbanCard>().CountAsync(x => x.LoopId == loopId, cancellationToken);
}
