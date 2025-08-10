using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IDepartmentsRepository
{
    Task<IReadOnlyCollection<Department>> BrowseAsync(CancellationToken cancellationToken = default);
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Department> AddAsync(Department department, CancellationToken cancellationToken = default);
    Task UpdateAsync(Department department, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
