using AsistOff.MES.Configuration.Domain.Entities;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IWarehousesRepository
{
    Task<IReadOnlyCollection<Warehouse>> BrowseAsync(CancellationToken cancellationToken = default);
    Task<Warehouse?> GetByIdAsync(Guid id,
        CancellationToken cancellationToken = default);
    Task<Warehouse> AddAsync(Warehouse warehouse,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(Warehouse warehouse,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id,
        CancellationToken cancellationToken = default);
}