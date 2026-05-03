using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.CustomerOrders.Domain.Repositories;

public interface ICustomersRepository
{
    Task<IReadOnlyCollection<Customer>> BrowseAsync(Paginator<Customer> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<Customer> predicate, CancellationToken cancellationToken = default);
    Task<Customer?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Customer> AddAsync(Customer entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Customer entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
}
