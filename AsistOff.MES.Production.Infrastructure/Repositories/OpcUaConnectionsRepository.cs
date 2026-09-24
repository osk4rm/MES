using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class OpcUaConnectionsRepository(DefaultContext context) : IOpcUaConnectionsRepository
{
    public async Task<IReadOnlyCollection<OpcUaConnection>> BrowseAsync(Paginator<OpcUaConnection> paginator, CancellationToken cancellationToken = default)
        => await context.Set<OpcUaConnection>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<OpcUaConnection> predicate, CancellationToken cancellationToken = default)
        => context.Set<OpcUaConnection>().Where(predicate).CountAsync(cancellationToken);

    public Task<OpcUaConnection?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<OpcUaConnection>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<OpcUaConnection?> GetByEndpointAsync(Guid machineId, string endpointUrl, CancellationToken cancellationToken = default)
        => context.Set<OpcUaConnection>()
            .FirstOrDefaultAsync(x => x.MachineId == machineId && x.EndpointUrl == endpointUrl, cancellationToken);

    public async Task<IReadOnlyCollection<OpcUaConnection>> ListAllAsync(CancellationToken cancellationToken = default)
        => await context.Set<OpcUaConnection>().ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<OpcUaConnection>> ListEnabledAsync(CancellationToken cancellationToken = default)
        => await context.Set<OpcUaConnection>().Where(x => x.IsEnabled).ToListAsync(cancellationToken);

    public async Task<OpcUaConnection> AddAsync(OpcUaConnection entity, CancellationToken cancellationToken = default)
    {
        context.Set<OpcUaConnection>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(OpcUaConnection entity, CancellationToken cancellationToken = default)
    {
        context.Set<OpcUaConnection>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<OpcUaConnection>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
