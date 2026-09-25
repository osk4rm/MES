using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IProductionConfirmationsRepository
{
    Task<IReadOnlyCollection<ProductionConfirmation>> BrowseAsync(Paginator<ProductionConfirmation> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<ProductionConfirmation> predicate, CancellationToken cancellationToken = default);
    Task<ProductionConfirmation?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// All confirmations reported against one order, oldest first.
    /// Runs under the tenant global query filter.
    /// </summary>
    Task<IReadOnlyCollection<ProductionConfirmation>> ListForOrderAsync(
        Guid productionOrderId, CancellationToken cancellationToken = default);
    /// <summary>
    /// All confirmations reported on one Work Center with <c>ReportedAt</c>
    /// inside the closed window, oldest first. Runs under the tenant global
    /// query filter.
    /// </summary>
    Task<IReadOnlyCollection<ProductionConfirmation>> ListForMachineInWindowAsync(
        Guid machineId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<ProductionConfirmation> AddAsync(ProductionConfirmation entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Read-time aggregates for one order. Runs under the tenant global query
    /// filter, so cross-tenant confirmations never affect the totals.
    /// </summary>
    Task<(decimal ProducedQuantity, decimal ScrappedQuantity, int ConfirmationsCount)> GetTotalsAsync(
        Guid productionOrderId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Batch variant for browse pages: one grouped query for all order ids.
    /// Orders without confirmations are absent from the dictionary.
    /// </summary>
    Task<Dictionary<Guid, (decimal ProducedQuantity, decimal ScrappedQuantity, int ConfirmationsCount)>> GetTotalsForOrdersAsync(
        IReadOnlyCollection<Guid> productionOrderIds, CancellationToken cancellationToken = default);
}
