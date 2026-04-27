using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Configuration.Domain.Repositories;

using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

public interface IDepartmentsRepository
{
    Task<IReadOnlyCollection<Department>> BrowseAsync(Paginator<Department> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<Department> predicate, CancellationToken cancellationToken = default);
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Department> AddAsync(Department department, CancellationToken cancellationToken = default);
    Task UpdateAsync(Department department, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
