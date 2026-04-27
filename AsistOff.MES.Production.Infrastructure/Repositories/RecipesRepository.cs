using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class RecipesRepository(DefaultContext context) : IRecipesRepository
{
    public async Task<IReadOnlyCollection<Recipe>> BrowseAsync(Paginator<Recipe> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<Recipe>()
            .AsNoTracking()
            .Include(x => x.Versions)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(ExpressionStarter<Recipe> predicate, CancellationToken cancellationToken = default)
        => context.Set<Recipe>().Where(predicate).CountAsync(cancellationToken);

    public Task<Recipe?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<Recipe>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Recipe?> GetWithVersionsAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<Recipe>()
            .AsNoTracking()
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Recipe> AddAsync(Recipe entity, CancellationToken cancellationToken = default)
    {
        context.Set<Recipe>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Recipe entity, CancellationToken cancellationToken = default)
    {
        context.Set<Recipe>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<Recipe>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<Recipe>()
            .AnyAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId), cancellationToken);
}
