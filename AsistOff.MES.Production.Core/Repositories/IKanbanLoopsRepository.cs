using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IKanbanLoopsRepository
{
    Task<IReadOnlyCollection<KanbanLoop>> BrowseAsync(Paginator<KanbanLoop> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<KanbanLoop> predicate, CancellationToken cancellationToken = default);
    Task<KanbanLoop?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KanbanLoop?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<KanbanLoop> AddAsync(KanbanLoop entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(KanbanLoop entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
}
