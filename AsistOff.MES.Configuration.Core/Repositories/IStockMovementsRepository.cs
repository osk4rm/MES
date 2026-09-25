using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IStockMovementsRepository
{
    Task<IReadOnlyCollection<StockMovement>> ListForConfirmationAsync(
        Guid productionConfirmationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// All ledger lines of the current tenant (the global query filter
    /// applies tenant isolation). Used by the read-only stock-on-hand
    /// aggregation; callers filter and group in memory.
    /// </summary>
    Task<IReadOnlyCollection<StockMovement>> ListAsync(
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<StockMovement> entities, CancellationToken cancellationToken = default);
}
