using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class MeasureUnitsRepository(DefaultContext context) 
    : IMeasureUnitsRepository
{
    public async Task<IReadOnlyCollection<MeasureUnit>> BrowseAsync(Paginator<MeasureUnit> paginator, CancellationToken cancellationToken = default)
    {
        var units = await context.MeasureUnits
            .Include(x => x.BaseUnit)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

        return units;
    }

    public Task<MeasureUnit?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return context.MeasureUnits
            .Include(x => x.BaseUnit)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<int> CountAsync(ExpressionStarter<MeasureUnit> predicate, CancellationToken cancellationToken = default)
    {
        return await context.MeasureUnits
            .Where(predicate)
            .CountAsync(cancellationToken);
    }

    public async Task<MeasureUnit> AddAsync(MeasureUnit entity, CancellationToken cancellationToken = default)
    {
        context.MeasureUnits.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(MeasureUnit entity, CancellationToken cancellationToken = default)
    {
        context.MeasureUnits.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.MeasureUnits
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
