using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface ILotsRepository
{
    Task<IReadOnlyCollection<Lot>> BrowseAsync(Paginator<Lot> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<Lot> predicate, CancellationToken cancellationToken = default);
    Task<Lot?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Lot?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Lot> AddAsync(Lot entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Lot entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
}
