using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IProductionOrdersRepository
{
    Task<IReadOnlyCollection<ProductionOrder>> BrowseAsync(Paginator<ProductionOrder> paginator, CancellationToken cancellationToken = default);
    /// <summary>
    /// Bounded dispatch-board read: Released/InProgress orders that are overdue,
    /// due inside the window or have no due date, overdue first, then due date
    /// (nulls last), priority, code, capped with Take. Runs AsNoTracking under
    /// the tenant global query filter.
    /// </summary>
    Task<IReadOnlyCollection<ProductionOrder>> BrowseDispatchAsync(DateOnly from, DateOnly to, int take, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<ProductionOrder> predicate, CancellationToken cancellationToken = default);
    Task<ProductionOrder?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductionOrder> AddAsync(ProductionOrder entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductionOrder entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
}
