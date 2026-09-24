using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class MachineTelemetryTagsRepository(DefaultContext context) : IMachineTelemetryTagsRepository
{
    public async Task<IReadOnlyCollection<MachineTelemetryTag>> BrowseAsync(Paginator<MachineTelemetryTag> paginator, CancellationToken cancellationToken = default)
        => await context.Set<MachineTelemetryTag>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<MachineTelemetryTag> predicate, CancellationToken cancellationToken = default)
        => context.Set<MachineTelemetryTag>().Where(predicate).CountAsync(cancellationToken);

    public Task<MachineTelemetryTag?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<MachineTelemetryTag>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<MachineTelemetryTag?> GetByNodeAsync(Guid machineId, string nodeId, CancellationToken cancellationToken = default)
        => context.Set<MachineTelemetryTag>()
            .FirstOrDefaultAsync(x => x.MachineId == machineId && x.NodeId == nodeId, cancellationToken);

    public async Task<MachineTelemetryTag> AddAsync(MachineTelemetryTag entity, CancellationToken cancellationToken = default)
    {
        context.Set<MachineTelemetryTag>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(MachineTelemetryTag entity, CancellationToken cancellationToken = default)
    {
        context.Set<MachineTelemetryTag>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<MachineTelemetryTag>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
