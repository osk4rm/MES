using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IShiftsRepository
{
    Task<IReadOnlyCollection<Shift>> BrowseAsync(
        Paginator<Shift> paginator,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ExpressionStarter<Shift> predicate,
        CancellationToken cancellationToken = default);

    Task<Shift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the shifts with the given ids. The tenant query filter guarantees
    /// that ids belonging to another tenant are simply not returned.
    /// </summary>
    Task<IReadOnlyCollection<Shift>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<Shift> AddAsync(Shift shift, CancellationToken cancellationToken = default);
    Task UpdateAsync(Shift shift, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
