using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Configuration.Domain.Repositories;

public interface IReasonCodesRepository
{
    Task<IReadOnlyCollection<ReasonCode>> BrowseAsync(
        Paginator<ReasonCode> paginator,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ExpressionStarter<ReasonCode> predicate,
        CancellationToken cancellationToken = default);

    Task<ReasonCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
    Task<ReasonCode> AddAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default);
    Task UpdateAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
