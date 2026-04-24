using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IOperationNodesRepository
{
    Task<OperationNode?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationNode?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OperationNode>> ListForVersionAsync(Guid recipeVersionId, CancellationToken cancellationToken = default);
    Task<OperationNode> AddAsync(OperationNode entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(OperationNode entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
