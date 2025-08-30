using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IProductGroupsRepository
{
    Task<IReadOnlyCollection<ProductGroup>> BrowseAsync(
        Paginator<ProductGroup> paginator,
        CancellationToken cancellationToken = default);
    
    Task<ProductGroup?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<ProductGroup> AddAsync(ProductGroup entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductGroup operatorEntity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}