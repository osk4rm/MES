using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class LotsRepository(DefaultContext context) : ILotsRepository
{
    public async Task<IReadOnlyCollection<Lot>> BrowseAsync(Paginator<Lot> paginator, CancellationToken cancellationToken = default)
        => await context.Set<Lot>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<Lot> predicate, CancellationToken cancellationToken = default)
        => context.Set<Lot>().Where(predicate).CountAsync(cancellationToken);

    public Task<Lot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<Lot>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Lot?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => context.Set<Lot>().FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

    public async Task<Lot> AddAsync(Lot entity, CancellationToken cancellationToken = default)
    {
        context.Set<Lot>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Lot entity, CancellationToken cancellationToken = default)
    {
        context.Set<Lot>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<Lot>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<Lot>()
            .AnyAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId), cancellationToken);
}
