using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface ITelemetryReadingsRepository
{
    Task<IReadOnlyCollection<TelemetryReading>> BrowseAsync(Paginator<TelemetryReading> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<TelemetryReading> predicate, CancellationToken cancellationToken = default);
    Task<TelemetryReading?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TelemetryReading> AddAsync(TelemetryReading entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest reading per tag from the filtered set (used by the
    /// latest-only browse mode). Readings sharing the same (tag, timestamp)
    /// may both be returned.
    /// </summary>
    Task<IReadOnlyCollection<TelemetryReading>> BrowseLatestAsync(ExpressionStarter<TelemetryReading> predicate, Paginator<TelemetryReading> paginator, CancellationToken cancellationToken = default);
    Task<int> CountLatestAsync(ExpressionStarter<TelemetryReading> predicate, CancellationToken cancellationToken = default);
}
