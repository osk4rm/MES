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

    /// <summary>Newest reading for one tag of the ambient tenant, if any.</summary>
    Task<TelemetryReading?> GetLatestAsync(Guid tagId, CancellationToken cancellationToken = default);

    /// <summary>Number of readings for one tag stored at or after <paramref name="since"/>.</summary>
    Task<int> CountSinceAsync(Guid tagId, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest reading per tag from the filtered set (used by the
    /// latest-only browse mode). Readings sharing the same (tag, timestamp)
    /// may both be returned.
    /// </summary>
    Task<IReadOnlyCollection<TelemetryReading>> BrowseLatestAsync(ExpressionStarter<TelemetryReading> predicate, Paginator<TelemetryReading> paginator, CancellationToken cancellationToken = default);
    Task<int> CountLatestAsync(ExpressionStarter<TelemetryReading> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Last <paramref name="take"/> readings of one tag in ascending time
    /// order (oldest first) for sparklines. The global tenant filter scopes
    /// the result to the caller tenant.
    /// </summary>
    Task<IReadOnlyCollection<TelemetryReading>> BrowseTrendAsync(Guid tagId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Up to <paramref name="take"/> readings matching <paramref name="predicate"/>
    /// in descending time order (newest first) for CSV export. When
    /// <paramref name="latestOnly"/> is true only the latest reading per tag
    /// is considered.
    /// </summary>
    Task<IReadOnlyCollection<TelemetryReading>> BrowseExportAsync(ExpressionStarter<TelemetryReading> predicate, bool latestOnly, int take, CancellationToken cancellationToken = default);
}
