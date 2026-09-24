using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IMachineTelemetryTagsRepository
{
    Task<IReadOnlyCollection<MachineTelemetryTag>> BrowseAsync(Paginator<MachineTelemetryTag> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<MachineTelemetryTag> predicate, CancellationToken cancellationToken = default);
    Task<MachineTelemetryTag?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MachineTelemetryTag?> GetByNodeAsync(Guid machineId, string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// All tags of the ambient tenant (the global query filter scopes the
    /// result). Used by the connection-status query.
    /// </summary>
    Task<IReadOnlyCollection<MachineTelemetryTag>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enabled tags of the ambient tenant. Used by the simulator poller.
    /// </summary>
    Task<IReadOnlyCollection<MachineTelemetryTag>> ListEnabledAsync(CancellationToken cancellationToken = default);
    Task<MachineTelemetryTag> AddAsync(MachineTelemetryTag entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(MachineTelemetryTag entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
