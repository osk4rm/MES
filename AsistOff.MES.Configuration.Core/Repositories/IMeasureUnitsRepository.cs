using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IMeasureUnitsRepository
{
    Task<IReadOnlyCollection<MeasureUnit>> BrowseAsync(
        Paginator<MeasureUnit> paginator,
        CancellationToken cancellationToken = default);
    
    Task<MeasureUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<MeasureUnit> AddAsync(MeasureUnit entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(MeasureUnit operatorEntity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
