using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IProductionOrdersRepository
{
    Task<IReadOnlyCollection<ProductionOrder>> BrowseAsync(Paginator<ProductionOrder> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<ProductionOrder> predicate, CancellationToken cancellationToken = default);
    Task<ProductionOrder?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductionOrder> AddAsync(ProductionOrder entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductionOrder entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
}
