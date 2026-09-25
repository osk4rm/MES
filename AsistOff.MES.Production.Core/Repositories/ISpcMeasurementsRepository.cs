using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface ISpcMeasurementsRepository
{
    Task<IReadOnlyCollection<SpcMeasurement>> BrowseAsync(
        Paginator<SpcMeasurement> paginator,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ExpressionStarter<SpcMeasurement> predicate,
        CancellationToken cancellationToken = default);

    Task<SpcMeasurement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// All measurements of one characteristic inside the optional closed
    /// window, in <c>MeasuredAt</c> ascending order. Runs under the tenant
    /// global query filter.
    /// </summary>
    Task<IReadOnlyCollection<SpcMeasurement>> ListForCharacteristicAsync(
        Guid characteristicId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);

    Task<SpcMeasurement> AddAsync(SpcMeasurement measurement, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
