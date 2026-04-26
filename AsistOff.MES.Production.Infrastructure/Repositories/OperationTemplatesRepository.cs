using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class OperationTemplatesRepository(DefaultContext context) : IOperationTemplatesRepository
{
    public async Task<IReadOnlyCollection<OperationTemplate>> BrowseAsync(Paginator<OperationTemplate> paginator, CancellationToken cancellationToken = default)
        => await context.Set<OperationTemplate>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<OperationTemplate> predicate, CancellationToken cancellationToken = default)
        => context.Set<OperationTemplate>().Where(predicate).CountAsync(cancellationToken);

    public Task<OperationTemplate?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<OperationTemplate>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<OperationTemplate> AddAsync(OperationTemplate entity, CancellationToken cancellationToken = default)
    {
        context.Set<OperationTemplate>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(OperationTemplate entity, CancellationToken cancellationToken = default)
    {
        context.Set<OperationTemplate>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<OperationTemplate>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<OperationTemplate>()
            .AnyAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId), cancellationToken);
}
