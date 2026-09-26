using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class ProductionOrdersRepository(DefaultContext context) : IProductionOrdersRepository
{
    public async Task<IReadOnlyCollection<ProductionOrder>> BrowseAsync(Paginator<ProductionOrder> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<ProductionOrder>()
            .AsNoTracking()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProductionOrder>> BrowseDispatchBoardAsync(DateOnly from, DateOnly to, int take, CancellationToken cancellationToken = default)
    {
        // DueDate is a timestamp; the board window is date-granular, so the
        // window maps to [from 00:00 UTC, day-after-to 00:00 UTC). Overdue
        // means strictly before the window start; null due dates are always
        // included. Ordering is overdue-first, then null due dates last, then
        // due date, priority, code — all translated to SQL with Take applied
        // before materialization (issue #274).
        var fromStartUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusiveUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return await context.Set<ProductionOrder>()
            .AsNoTracking()
            .Where(x => x.Status == ProductionOrderStatus.Released || x.Status == ProductionOrderStatus.InProgress)
            .Where(x => x.DueDate == null
                || x.DueDate < fromStartUtc
                || (x.DueDate >= fromStartUtc && x.DueDate < toExclusiveUtc))
            .OrderByDescending(x => x.DueDate != null && x.DueDate < fromStartUtc)
            .ThenBy(x => x.DueDate == null)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Code)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(ExpressionStarter<ProductionOrder> predicate, CancellationToken cancellationToken = default)
        => context.Set<ProductionOrder>().Where(predicate).CountAsync(cancellationToken);

    public Task<ProductionOrder?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<ProductionOrder>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<ProductionOrder> AddAsync(ProductionOrder entity, CancellationToken cancellationToken = default)
    {
        context.Set<ProductionOrder>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ProductionOrder entity, CancellationToken cancellationToken = default)
    {
        context.Set<ProductionOrder>().Update(entity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Two writers passed the handler-level token check at the same
            // time; the xmin predicate rejected the loser (issue #263).
            // Detach the stale entries and report 409 with the current token.
            foreach (var entry in ex.Entries)
                entry.State = EntityState.Detached;

            var current = await context.Set<ProductionOrder>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);

            if (current is null)
                throw new NotFoundException("ProductionOrder", entity.Id);

            throw new ConcurrencyConflictException(
                current.Xmin.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"Production order '{entity.Code}' was modified by another user. Reload the order and retry with the current concurrency token.");
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Tracked remove (not ExecuteDeleteAsync) so the AuditHistoryInterceptor
        // observes the delete and appends the history row in the same transaction.
        var order = await context.Set<ProductionOrder>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null)
        {
            return;
        }

        context.Set<ProductionOrder>().Remove(order);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
        => context.Set<ProductionOrder>()
            .AnyAsync(x => x.Code == code && (excludeId == null || x.Id != excludeId), cancellationToken);
}
