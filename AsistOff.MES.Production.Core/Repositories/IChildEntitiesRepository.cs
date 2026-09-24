using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IChildEntitiesRepository
{
    Task<BomItem?> GetBomItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationOutput?> GetOperationOutputAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ResourceRequirement?> GetResourceRequirementAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddBomItemAsync(BomItem entity, CancellationToken cancellationToken = default);
    Task AddOperationOutputAsync(OperationOutput entity, CancellationToken cancellationToken = default);
    Task AddResourceRequirementAsync(ResourceRequirement entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the highest <c>SortIndex</c> currently used by BOM items
    /// belonging to the given operation, or <c>null</c> if none exist.
    /// </summary>
    Task<int?> GetMaxBomItemSortIndexAsync(Guid operationNodeId, CancellationToken cancellationToken = default);

    Task RemoveBomItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task RemoveOperationOutputAsync(Guid id, CancellationToken cancellationToken = default);
    Task RemoveResourceRequirementAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every BOM item in the given recipe version (across all its operations),
    /// ordered for preview display. Runs under the tenant global query filter.
    /// </summary>
    Task<IReadOnlyCollection<BomItem>> ListBomItemsForVersionAsync(
        Guid recipeVersionId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
