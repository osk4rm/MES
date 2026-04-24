using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class RecipeVersionsRepository(DefaultContext context) : IRecipeVersionsRepository
{
    public Task<RecipeVersion?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<RecipeVersion>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<RecipeVersion?> GetFullAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<RecipeVersion>()
            .Include(x => x.Operations).ThenInclude(o => o.Dependencies)
            .Include(x => x.Operations).ThenInclude(o => o.BomItems)
            .Include(x => x.Operations).ThenInclude(o => o.Outputs)
            .Include(x => x.Operations).ThenInclude(o => o.ResourceRequirements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<RecipeVersion>> ListForRecipeAsync(Guid recipeId, CancellationToken cancellationToken = default)
        => await context.Set<RecipeVersion>()
            .Where(x => x.RecipeId == recipeId)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

    public async Task<int> GetNextVersionNumberAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        var max = await context.Set<RecipeVersion>()
            .Where(x => x.RecipeId == recipeId)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    public async Task<RecipeVersion> AddAsync(RecipeVersion entity, CancellationToken cancellationToken = default)
    {
        context.Set<RecipeVersion>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(RecipeVersion entity, CancellationToken cancellationToken = default)
    {
        context.Set<RecipeVersion>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<RecipeVersion>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
