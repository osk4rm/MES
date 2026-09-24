using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class SpcCharacteristicsRepository(DefaultContext context) : ISpcCharacteristicsRepository
{
    public async Task<IReadOnlyCollection<SpcCharacteristic>> BrowseAsync(
        Paginator<SpcCharacteristic> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<SpcCharacteristic>()
            .AsNoTracking()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        ExpressionStarter<SpcCharacteristic> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<SpcCharacteristic>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<SpcCharacteristic?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<SpcCharacteristic>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        string code, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var query = context.Set<SpcCharacteristic>().Where(x => x.Code == code);
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<SpcCharacteristic> AddAsync(SpcCharacteristic characteristic, CancellationToken cancellationToken = default)
    {
        context.Set<SpcCharacteristic>().Add(characteristic);
        await context.SaveChangesAsync(cancellationToken);
        return characteristic;
    }

    public async Task UpdateAsync(SpcCharacteristic characteristic, CancellationToken cancellationToken = default)
    {
        context.Set<SpcCharacteristic>().Update(characteristic);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<SpcCharacteristic>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
