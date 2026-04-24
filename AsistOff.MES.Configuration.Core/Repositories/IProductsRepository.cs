using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IProductsRepository
{
    Task<IReadOnlyCollection<Product>> BrowseAsync(
        Paginator<Product> paginator,
        CancellationToken cancellationToken = default);
    
    Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<Product> predicate, CancellationToken cancellationToken = default);
    Task<Product> AddAsync(Product entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Product entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}