using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IKanbanCardsRepository
{
    Task<IReadOnlyCollection<KanbanCard>> BrowseAsync(Paginator<KanbanCard> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<KanbanCard> predicate, CancellationToken cancellationToken = default);
    Task<KanbanCard?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KanbanCard> AddAsync(KanbanCard entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(KanbanCard entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CardNumberExistsAsync(Guid loopId, string cardNumber, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<int> CountByLoopAsync(Guid loopId, CancellationToken cancellationToken = default);
}
