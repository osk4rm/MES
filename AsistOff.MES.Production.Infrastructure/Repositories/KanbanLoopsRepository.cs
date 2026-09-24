using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class KanbanLoopsRepository(DefaultContext context) : IKanbanLoopsRepository
{
    public async Task<IReadOnlyCollection<KanbanLoop>> BrowseAsync(Paginator<KanbanLoop> paginator, CancellationToken cancellationToken = default)
        => await context.Set<KanbanLoop>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<KanbanLoop> predicate, CancellationToken cancellationToken = default)
        => context.Set<KanbanLoop>().Where(predicate).CountAsync(cancellationToken);

    public Task<KanbanLoop?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<KanbanLoop>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<KanbanLoop?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => context.Set<KanbanLoop>().FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

    public async Task<KanbanLoop> AddAsync(KanbanLoop entity, CancellationToken cancellationToken = default)
    {
        context.Set<KanbanLoop>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(KanbanLoop entity, CancellationToken cancellationToken = default)
    {
        context.Set<KanbanLoop>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<KanbanLoop>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<KanbanLoop>()
            .AnyAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId), cancellationToken);
}
