using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface ILotGenealogyEdgesRepository
{
    Task<IReadOnlyCollection<LotGenealogyEdge>> BrowseAsync(Paginator<LotGenealogyEdge> paginator, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ExpressionStarter<LotGenealogyEdge> predicate, CancellationToken cancellationToken = default);
    Task<LotGenealogyEdge?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LotGenealogyEdge> AddAsync(LotGenealogyEdge entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LotGenealogyEdge>> ListByProducedLotIdsAsync(IReadOnlyCollection<Guid> producedLotIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LotGenealogyEdge>> ListByConsumedLotIdsAsync(IReadOnlyCollection<Guid> consumedLotIds, CancellationToken cancellationToken = default);
    /// <summary>
    /// Deletes the auto-posted edges of one confirmation in code, so the
    /// delete plus re-create correction model stays consistent. Runs under
    /// the tenant global query filter.
    /// </summary>
    Task DeleteByConfirmationAsync(Guid productionConfirmationId, CancellationToken cancellationToken = default);
}
