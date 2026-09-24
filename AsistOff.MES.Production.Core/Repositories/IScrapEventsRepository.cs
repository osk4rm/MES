using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IScrapEventsRepository
{
    Task<IReadOnlyCollection<ScrapEvent>> BrowseAsync(Paginator<ScrapEvent> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<ScrapEvent> predicate, CancellationToken cancellationToken = default);
    Task<ScrapEvent?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// All scrap rows reported on one Work Center with <c>ReportedAt</c>
    /// inside the closed window, oldest first. Runs under the tenant global
    /// query filter.
    /// </summary>
    Task<IReadOnlyCollection<ScrapEvent>> ListForMachineInWindowAsync(
        Guid machineId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<ScrapEvent> AddAsync(ScrapEvent entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ScrapEvent entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
