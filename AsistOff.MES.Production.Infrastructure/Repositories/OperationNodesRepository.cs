using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class OperationNodesRepository(DefaultContext context) : IOperationNodesRepository
{
    public Task<OperationNode?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<OperationNode>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<OperationNode?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<OperationNode>()
            .Include(x => x.Dependencies)
            .Include(x => x.BomItems)
            .Include(x => x.Outputs)
            .Include(x => x.ResourceRequirements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<OperationNode>> ListForVersionAsync(Guid recipeVersionId, CancellationToken cancellationToken = default)
        => await context.Set<OperationNode>()
            .Where(x => x.RecipeVersionId == recipeVersionId)
            .OrderBy(x => x.SortIndex)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<OperationNode>> ListForVersionWithDependenciesAsync(Guid recipeVersionId, CancellationToken cancellationToken = default)
        => await context.Set<OperationNode>()
            .Where(x => x.RecipeVersionId == recipeVersionId)
            .Include(x => x.Dependencies)
            .OrderBy(x => x.SortIndex)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<OperationNode>> ListForVersionsAsync(
        IReadOnlyCollection<Guid> recipeVersionIds, CancellationToken cancellationToken = default)
    {
        if (recipeVersionIds.Count == 0)
            return [];

        // Read-only Gantt input: dependencies drive chaining, resource
        // requirements drive the Work Center (machine) assignment. Tenant
        // isolation comes from the global query filter.
        return await context.Set<OperationNode>()
            .AsNoTracking()
            .Where(x => recipeVersionIds.Contains(x.RecipeVersionId))
            .Include(x => x.Dependencies)
            .Include(x => x.ResourceRequirements)
            .OrderBy(x => x.RecipeVersionId)
            .ThenBy(x => x.SortIndex)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationNode> AddAsync(OperationNode entity, CancellationToken cancellationToken = default)
    {
        context.Set<OperationNode>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(OperationNode entity, CancellationToken cancellationToken = default)
    {
        context.Set<OperationNode>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<OperationNode>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
