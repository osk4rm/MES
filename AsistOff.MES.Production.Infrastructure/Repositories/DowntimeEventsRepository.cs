using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class DowntimeEventsRepository(DefaultContext context) : IDowntimeEventsRepository
{
    public async Task<IReadOnlyCollection<DowntimeEvent>> BrowseAsync(Paginator<DowntimeEvent> paginator, CancellationToken cancellationToken = default)
        => await context.Set<DowntimeEvent>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<DowntimeEvent> predicate, CancellationToken cancellationToken = default)
        => context.Set<DowntimeEvent>().Where(predicate).CountAsync(cancellationToken);

    public Task<DowntimeEvent?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<DowntimeEvent>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<DowntimeEvent> AddAsync(DowntimeEvent entity, CancellationToken cancellationToken = default)
    {
        context.Set<DowntimeEvent>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(DowntimeEvent entity, CancellationToken cancellationToken = default)
    {
        context.Set<DowntimeEvent>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<DowntimeEvent>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> HasOpenEventAsync(Guid machineId, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<DowntimeEvent>()
            .AnyAsync(
                x => x.MachineId == machineId
                    && x.EndedAt == null
                    && (excludeId == null || x.Id != excludeId),
                cancellationToken);
}
