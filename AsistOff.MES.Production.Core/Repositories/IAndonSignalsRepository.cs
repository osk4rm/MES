using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IAndonSignalsRepository
{
    Task<IReadOnlyCollection<AndonSignal>> BrowseAsync(Paginator<AndonSignal> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<AndonSignal> predicate, CancellationToken cancellationToken = default);
    Task<AndonSignal?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AndonSignal> AddAsync(AndonSignal entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AndonSignal entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasActiveSignalAsync(Guid machineId, Guid? excludeId, CancellationToken cancellationToken = default);
}
