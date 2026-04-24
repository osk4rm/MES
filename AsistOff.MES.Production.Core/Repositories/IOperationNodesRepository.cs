using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IOperationNodesRepository
{
    Task<OperationNode?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationNode?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OperationNode>> ListForVersionAsync(Guid recipeVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every operation in the given recipe version with its
    /// <see cref="OperationNode.Dependencies"/> collection eagerly loaded.
    /// Required for whole-graph operations (e.g. DAG cycle detection across
    /// edges that belong to siblings of the operation being mutated).
    /// </summary>
    Task<IReadOnlyCollection<OperationNode>> ListForVersionWithDependenciesAsync(Guid recipeVersionId, CancellationToken cancellationToken = default);
    Task<OperationNode> AddAsync(OperationNode entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(OperationNode entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
