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
    Task UpdateAsync(DowntimeEvent entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasOpenEventAsync(Guid machineId, Guid? excludeId, CancellationToken cancellationToken = default);
}
