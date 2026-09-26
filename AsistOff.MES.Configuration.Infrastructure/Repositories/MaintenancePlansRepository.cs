using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class MaintenancePlansRepository(DefaultContext context) : IMaintenancePlansRepository
{
    public async Task<IReadOnlyCollection<MaintenancePlan>> BrowseAsync(
        Paginator<MaintenancePlan> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<MaintenancePlan>()
            .Include(x => x.Machine)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        ExpressionStarter<MaintenancePlan> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<MaintenancePlan>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<MaintenancePlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<MaintenancePlan>()
            .Include(x => x.Machine)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MaintenancePlan>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Set<MaintenancePlan>()
            .Include(x => x.Machine)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        string code, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var query = context.Set<MaintenancePlan>().Where(x => x.Code == code);
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<MaintenancePlan> AddAsync(
        MaintenancePlan plan, CancellationToken cancellationToken = default)
    {
        context.Set<MaintenancePlan>().Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task UpdateAsync(MaintenancePlan plan, CancellationToken cancellationToken = default)
    {
        context.Set<MaintenancePlan>().Update(plan);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Tracked remove (not ExecuteDeleteAsync) so the AuditHistoryInterceptor
        // observes the delete and appends the history row in the same transaction.
        var plan = await context.Set<MaintenancePlan>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null)
        {
            return;
        }

        context.Set<MaintenancePlan>().Remove(plan);
        await context.SaveChangesAsync(cancellationToken);
    }
}
