using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IMaintenanceWorkOrdersRepository
{
    Task<IReadOnlyCollection<MaintenanceWorkOrder>> BrowseAsync(
        Paginator<MaintenanceWorkOrder> paginator,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ExpressionStarter<MaintenanceWorkOrder> predicate,
        CancellationToken cancellationToken = default);

    Task<MaintenanceWorkOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<MaintenanceWorkOrder> AddAsync(MaintenanceWorkOrder workOrder, CancellationToken cancellationToken = default);
    Task UpdateAsync(MaintenanceWorkOrder workOrder, CancellationToken cancellationToken = default);
}
