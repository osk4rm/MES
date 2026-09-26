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
    /// <summary>
    /// Open or InProgress work orders raised from the given preventive plans.
    /// Used as the idempotency guard for due evaluation: a plan with a live
    /// order must not raise a duplicate. Runs under the tenant global query
    /// filter.
    /// </summary>
    Task<IReadOnlyCollection<MaintenanceWorkOrder>> ListOpenByPlanIdsAsync(
        IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default);
    /// <summary>
    /// Done work orders of one Work Center whose <c>CompletedAt</c> falls
    /// inside the window (inclusive). Runs under the tenant global query
    /// filter. Non-Done rows and rows with null <c>CompletedAt</c> never
    /// count toward repair KPIs.
    /// </summary>
    Task<IReadOnlyCollection<MaintenanceWorkOrder>> ListDoneInWindowAsync(
        Guid machineId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<MaintenanceWorkOrder> AddAsync(MaintenanceWorkOrder workOrder, CancellationToken cancellationToken = default);
    Task UpdateAsync(MaintenanceWorkOrder workOrder, CancellationToken cancellationToken = default);
}
