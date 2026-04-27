using AsistOff.MES.Production.Domain.Entities;

namespace AsistOff.MES.Production.Domain.Repositories;

public interface IRecipeVersionsRepository
{
    Task<RecipeVersion?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a version with the full aggregate: operations, dependencies, bom items, outputs, resource requirements.</summary>
    Task<RecipeVersion?> GetFullAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RecipeVersion>> ListForRecipeAsync(Guid recipeId, CancellationToken cancellationToken = default);
    Task<int> GetNextVersionNumberAsync(Guid recipeId, CancellationToken cancellationToken = default);
    Task<RecipeVersion> AddAsync(RecipeVersion entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(RecipeVersion entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Saves pending changes in the underlying context (used to commit graph-wide edits inside handlers).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
