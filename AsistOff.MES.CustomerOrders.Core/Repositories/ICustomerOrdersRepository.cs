using AsistOff.MES.CustomerOrders.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.CustomerOrders.Domain.Repositories;

public interface ICustomerOrdersRepository
{
    Task<IReadOnlyCollection<CustomerOrder>> BrowseAsync(Paginator<CustomerOrder> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<CustomerOrder> predicate, CancellationToken cancellationToken = default);
    Task<CustomerOrder?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerOrder?> GetWithLinesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerOrder> AddAsync(CustomerOrder entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomerOrder entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> OrderNumberExistsAsync(string orderNumber, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<bool> ExternalOrderExistsAsync(string externalSystem, string externalOrderId, Guid? excludeId, CancellationToken cancellationToken = default);
}
