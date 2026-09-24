using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class ScrapEventsRepository(DefaultContext context) : IScrapEventsRepository
{
    public async Task<IReadOnlyCollection<ScrapEvent>> BrowseAsync(Paginator<ScrapEvent> paginator, CancellationToken cancellationToken = default)
        => await context.Set<ScrapEvent>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<ScrapEvent> predicate, CancellationToken cancellationToken = default)
        => context.Set<ScrapEvent>().Where(predicate).CountAsync(cancellationToken);

    public Task<ScrapEvent?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<ScrapEvent>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<ScrapEvent> AddAsync(ScrapEvent entity, CancellationToken cancellationToken = default)
    {
        context.Set<ScrapEvent>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ScrapEvent entity, CancellationToken cancellationToken = default)
    {
        context.Set<ScrapEvent>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<ScrapEvent>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
