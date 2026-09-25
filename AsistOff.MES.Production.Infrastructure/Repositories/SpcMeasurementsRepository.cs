using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class SpcMeasurementsRepository(DefaultContext context) : ISpcMeasurementsRepository
{
    public async Task<IReadOnlyCollection<SpcMeasurement>> BrowseAsync(
        Paginator<SpcMeasurement> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<SpcMeasurement>()
            .AsNoTracking()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        ExpressionStarter<SpcMeasurement> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<SpcMeasurement>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<SpcMeasurement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<SpcMeasurement>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<SpcMeasurement>> ListForCharacteristicAsync(
        Guid characteristicId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var query = context.Set<SpcMeasurement>()
            .AsNoTracking()
            .Where(x => x.CharacteristicId == characteristicId);
        if (fromUtc.HasValue)
            query = query.Where(x => x.MeasuredAt >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(x => x.MeasuredAt <= toUtc.Value);
        return await query
            .OrderBy(x => x.MeasuredAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<SpcMeasurement> AddAsync(SpcMeasurement measurement, CancellationToken cancellationToken = default)
    {
        context.Set<SpcMeasurement>().Add(measurement);
        await context.SaveChangesAsync(cancellationToken);
        return measurement;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<SpcMeasurement>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
