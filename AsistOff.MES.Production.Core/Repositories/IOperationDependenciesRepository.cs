using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Domain.Repositories;

/// <summary>
/// Repository for <see cref="OperationDependency"/> edges. Kept separate from
/// <see cref="IOperationNodesRepository"/> so that dependency mutations operate
/// directly on the <c>DbSet</c> rather than through the navigation collection of
/// a tracked principal — see <see cref="ListForOperationAsync"/> for context.
/// </summary>
public interface IOperationDependenciesRepository
{
    /// <summary>
    /// Returns the existing dependency edges for a single operation, tracked by
    /// the underlying context (so callers can update or remove them in place).
    /// </summary>
    Task<IReadOnlyCollection<OperationDependency>> ListForOperationAsync(
        Guid operationNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight projection of all dependency edges in a recipe version,
    /// suitable for whole-graph cycle detection. No tracking, no entity hydration.
    /// </summary>
    Task<IReadOnlyCollection<DependencyEdge>> ListEdgesForVersionAsync(
        Guid recipeVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight Id projection for validating predecessor identities during a Set call.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> ListOperationIdsForVersionAsync(
        Guid recipeVersionId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new edge for insertion. Caller must invoke <see cref="SaveChangesAsync"/>.</summary>
    void Add(OperationDependency entity);

    /// <summary>Stages a tracked edge for deletion. Caller must invoke <see cref="SaveChangesAsync"/>.</summary>
    void Remove(OperationDependency entity);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Directed edge in the operation DAG: <see cref="Successor"/> depends on <see cref="Predecessor"/>.
/// </summary>
public readonly record struct DependencyEdge(Guid Successor, Guid Predecessor);
