using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class AndonSignalsRepository(DefaultContext context) : IAndonSignalsRepository
{
    public async Task<IReadOnlyCollection<AndonSignal>> BrowseAsync(Paginator<AndonSignal> paginator, CancellationToken cancellationToken = default)
        => await context.Set<AndonSignal>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<AndonSignal> predicate, CancellationToken cancellationToken = default)
        => context.Set<AndonSignal>().Where(predicate).CountAsync(cancellationToken);

    public Task<AndonSignal?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<AndonSignal>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<AndonSignal> AddAsync(AndonSignal entity, CancellationToken cancellationToken = default)
    {
        context.Set<AndonSignal>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(AndonSignal entity, CancellationToken cancellationToken = default)
    {
        context.Set<AndonSignal>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<AndonSignal>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> HasActiveSignalAsync(Guid machineId, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<AndonSignal>()
            .AnyAsync(x => x.MachineId == machineId
                && x.Status == Domain.Enums.AndonSignalStatus.Active
                && (excludeId == null || x.Id != excludeId), cancellationToken);
}
