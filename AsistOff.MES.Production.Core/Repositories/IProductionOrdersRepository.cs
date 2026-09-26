using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IProductionOrdersRepository
{
    Task<IReadOnlyCollection<ProductionOrder>> BrowseAsync(Paginator<ProductionOrder> paginator, CancellationToken cancellationToken = default);
    /// <summary>
    /// Bounded server-side read for the shift-aware dispatch board (issue #274):
    /// Released and InProgress orders that are overdue, due inside
    /// <c>[from, to]</c> or have no due date, ordered overdue-first then due
    /// date (nulls last), priority, code, capped at <c>take</c> rows. Runs
    /// under the tenant global query filter with no change tracking.
    /// </summary>
    Task<IReadOnlyCollection<ProductionOrder>> BrowseDispatchBoardAsync(DateOnly from, DateOnly to, int take, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<ProductionOrder> predicate, CancellationToken cancellationToken = default);
    Task<ProductionOrder?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductionOrder> AddAsync(ProductionOrder entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductionOrder entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
}
