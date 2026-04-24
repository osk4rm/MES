using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IMachinesRepository
{
    Task<IReadOnlyCollection<Machine>> BrowseAsync(Paginator<Machine> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<Machine> predicate, CancellationToken cancellationToken = default);
    Task<Machine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Machine> AddAsync(Machine machine, CancellationToken cancellationToken = default);
    Task UpdateAsync(Machine machine, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
