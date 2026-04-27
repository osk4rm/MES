using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IWarehousesRepository
{
    Task<IReadOnlyCollection<Warehouse>> BrowseAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Warehouse>> BrowseAsync(Paginator<Warehouse> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<Warehouse> predicate, CancellationToken cancellationToken = default);
    Task<Warehouse?> GetByIdAsync(Guid id,
        CancellationToken cancellationToken = default);
    Task<Warehouse> AddAsync(Warehouse warehouse,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(Warehouse warehouse,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id,
        CancellationToken cancellationToken = default);
}