using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IMaterialReservationsRepository
{
    Task<MaterialReservation?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every reservation row of one Production Order in FIFO creation order
    /// (oldest first). Runs under the tenant global query filter.
    /// </summary>
    Task<IReadOnlyCollection<MaterialReservation>> ListForOrderAsync(
        Guid productionOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// All non-Closed reservations of the current tenant. Used by the
    /// stock-on-hand availability computation; callers group in memory,
    /// mirroring the <see cref="IStockMovementsRepository"/> pattern.
    /// </summary>
    Task<IReadOnlyCollection<MaterialReservation>> ListOpenAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MaterialReservation>> BrowseAsync(
        Paginator<MaterialReservation> paginator, CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ExpressionStarter<MaterialReservation> predicate, CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<MaterialReservation> entities, CancellationToken cancellationToken = default);

    Task UpdateAsync(MaterialReservation entity, CancellationToken cancellationToken = default);
}
