using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class TelemetryReadingsRepository(DefaultContext context) : ITelemetryReadingsRepository
{
    public async Task<IReadOnlyCollection<TelemetryReading>> BrowseAsync(Paginator<TelemetryReading> paginator, CancellationToken cancellationToken = default)
        => await context.Set<TelemetryReading>()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<TelemetryReading> predicate, CancellationToken cancellationToken = default)
        => context.Set<TelemetryReading>().Where(predicate).CountAsync(cancellationToken);

    public Task<TelemetryReading?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<TelemetryReading>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<TelemetryReading> AddAsync(TelemetryReading entity, CancellationToken cancellationToken = default)
    {
        context.Set<TelemetryReading>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyCollection<TelemetryReading>> BrowseLatestAsync(ExpressionStarter<TelemetryReading> predicate, Paginator<TelemetryReading> paginator, CancellationToken cancellationToken = default)
    {
        var filtered = context.Set<TelemetryReading>().Where(predicate);
        var latestPerTag = filtered
            .GroupBy(r => r.TagId)
            .Select(g => new { TagId = g.Key, ReadAt = g.Max(r => r.ReadAt) });

        var query = filtered.Join(
            latestPerTag,
            reading => new { reading.TagId, reading.ReadAt },
            latest => new { latest.TagId, latest.ReadAt },
            (reading, _) => reading);

        return await query
            .Sort(paginator.Paging)
            .Page(paginator.Paging)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountLatestAsync(ExpressionStarter<TelemetryReading> predicate, CancellationToken cancellationToken = default)
        => context.Set<TelemetryReading>()
            .Where(predicate)
            .Select(r => r.TagId)
            .Distinct()
            .CountAsync(cancellationToken);
}
