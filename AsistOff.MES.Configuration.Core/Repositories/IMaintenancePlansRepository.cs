using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IMaintenancePlansRepository
{
    Task<IReadOnlyCollection<MaintenancePlan>> BrowseAsync(
        Paginator<MaintenancePlan> paginator,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ExpressionStarter<MaintenancePlan> predicate,
        CancellationToken cancellationToken = default);

    Task<MaintenancePlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// All active preventive plans for the current tenant (global query filter
    /// applies). Due selection (Time vs Meter) happens in the application
    /// handler so unit tests can cover the rules without EF Core.
    /// </summary>
    Task<IReadOnlyCollection<MaintenancePlan>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<MaintenancePlan> AddAsync(MaintenancePlan plan, CancellationToken cancellationToken = default);
    Task UpdateAsync(MaintenancePlan plan, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
