using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IScrapEventsRepository
{
    Task<IReadOnlyCollection<ScrapEvent>> BrowseAsync(Paginator<ScrapEvent> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<ScrapEvent> predicate, CancellationToken cancellationToken = default);
    Task<ScrapEvent?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ScrapEvent> AddAsync(ScrapEvent entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ScrapEvent entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
