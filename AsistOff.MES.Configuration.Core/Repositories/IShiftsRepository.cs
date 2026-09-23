using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IShiftsRepository
{
    Task<IReadOnlyCollection<Shift>> BrowseAsync(Paginator<Shift> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<Shift> predicate, CancellationToken cancellationToken = default);
    Task<Shift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<Shift> AddAsync(Shift shift, CancellationToken cancellationToken = default);
    Task UpdateAsync(Shift shift, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
