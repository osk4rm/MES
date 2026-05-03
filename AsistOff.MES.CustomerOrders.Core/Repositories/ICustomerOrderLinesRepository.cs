using AsistOff.MES.CustomerOrders.Domain.Entities;

namespace AsistOff.MES.CustomerOrders.Domain.Repositories;

public interface ICustomerOrderLinesRepository
{
    Task<CustomerOrderLine?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerOrderLine?> GetWithOrderAndReleasesAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(CustomerOrderLine entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomerOrderLine entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddProductionReleaseAsync(CustomerOrderLineProductionRelease entity, CancellationToken cancellationToken = default);
}
