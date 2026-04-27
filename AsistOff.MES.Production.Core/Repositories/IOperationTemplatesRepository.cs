using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IOperationTemplatesRepository
{
    Task<IReadOnlyCollection<OperationTemplate>> BrowseAsync(Paginator<OperationTemplate> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<OperationTemplate> predicate, CancellationToken cancellationToken = default);
    Task<OperationTemplate?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationTemplate> AddAsync(OperationTemplate entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(OperationTemplate entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);
}
