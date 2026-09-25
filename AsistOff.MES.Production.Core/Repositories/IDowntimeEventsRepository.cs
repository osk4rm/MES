using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IDowntimeEventsRepository
{
    Task<IReadOnlyCollection<DowntimeEvent>> BrowseAsync(Paginator<DowntimeEvent> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<DowntimeEvent> predicate, CancellationToken cancellationToken = default);
    Task<DowntimeEvent?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DowntimeEvent> AddAsync(DowntimeEvent entity, CancellationToken cancellationToken = default);
    /// <summary>
    /// All events of one Work Center overlapping the window, open events
    /// included. Runs under the tenant global query filter. The caller
    /// decides how open events are treated (the OEE snapshot ignores them).
    /// </summary>
    Task<IReadOnlyCollection<DowntimeEvent>> ListOverlappingAsync(
        Guid machineId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task UpdateAsync(DowntimeEvent entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasOpenEventAsync(Guid machineId, Guid? excludeId, CancellationToken cancellationToken = default);
}
