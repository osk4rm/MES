using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IProductionConfirmationsRepository
{
    Task<IReadOnlyCollection<ProductionConfirmation>> BrowseAsync(Paginator<ProductionConfirmation> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<ProductionConfirmation> predicate, CancellationToken cancellationToken = default);
    Task<ProductionConfirmation?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductionConfirmation> AddAsync(ProductionConfirmation entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
