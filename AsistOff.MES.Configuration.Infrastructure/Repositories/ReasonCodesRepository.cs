using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class ReasonCodesRepository(DefaultContext context) : IReasonCodesRepository
{
    public async Task<IReadOnlyCollection<ReasonCode>> BrowseAsync(
        Paginator<ReasonCode> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<ReasonCode>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        ExpressionStarter<ReasonCode> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<ReasonCode>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<ReasonCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<ReasonCode>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        string code, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var query = context.Set<ReasonCode>().Where(x => x.Code == code);
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<ReasonCode> AddAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default)
    {
        context.Set<ReasonCode>().Add(reasonCode);
        await context.SaveChangesAsync(cancellationToken);
        return reasonCode;
    }

    public async Task UpdateAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default)
    {
        context.Set<ReasonCode>().Update(reasonCode);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<ReasonCode>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
