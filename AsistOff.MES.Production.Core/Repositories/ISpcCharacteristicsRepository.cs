using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface ISpcCharacteristicsRepository
{
    Task<IReadOnlyCollection<SpcCharacteristic>> BrowseAsync(
        Paginator<SpcCharacteristic> paginator,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ExpressionStarter<SpcCharacteristic> predicate,
        CancellationToken cancellationToken = default);

    Task<SpcCharacteristic?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<SpcCharacteristic> AddAsync(SpcCharacteristic characteristic, CancellationToken cancellationToken = default);
    Task UpdateAsync(SpcCharacteristic characteristic, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
