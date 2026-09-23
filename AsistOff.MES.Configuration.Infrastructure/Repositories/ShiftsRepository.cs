using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class ShiftsRepository(DefaultContext context) : IShiftsRepository
{
    public async Task<IReadOnlyCollection<Shift>> BrowseAsync(Paginator<Shift> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<Shift>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(ExpressionStarter<Shift> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<Shift>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<Shift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<Shift>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var q = context.Set<Shift>().Where(x => x.Code == code);
        if (excludeId.HasValue)
            q = q.Where(x => x.Id != excludeId.Value);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task<Shift> AddAsync(Shift shift, CancellationToken cancellationToken = default)
    {
        context.Set<Shift>().Add(shift);
        await context.SaveChangesAsync(cancellationToken);
        return shift;
    }

    public async Task UpdateAsync(Shift shift, CancellationToken cancellationToken = default)
    {
        context.Set<Shift>().Update(shift);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<Shift>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
