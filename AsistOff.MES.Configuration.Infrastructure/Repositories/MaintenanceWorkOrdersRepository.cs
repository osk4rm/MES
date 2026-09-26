using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class MaintenanceWorkOrdersRepository(DefaultContext context) : IMaintenanceWorkOrdersRepository
{
    public async Task<IReadOnlyCollection<MaintenanceWorkOrder>> BrowseAsync(
        Paginator<MaintenanceWorkOrder> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<MaintenanceWorkOrder>()
            .Include(x => x.Machine)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        ExpressionStarter<MaintenanceWorkOrder> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<MaintenanceWorkOrder>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<MaintenanceWorkOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<MaintenanceWorkOrder>()
            .Include(x => x.Machine)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MaintenanceWorkOrder>> ListOpenByPlanIdsAsync(
        IReadOnlyCollection<Guid> planIds, CancellationToken cancellationToken = default)
    {
        if (planIds.Count == 0)
            return Array.Empty<MaintenanceWorkOrder>();

        return await context.Set<MaintenanceWorkOrder>()
            .AsNoTracking()
            .Where(x => x.PlanId != null
                && planIds.Contains(x.PlanId.Value)
                && (x.Status == MaintenanceWorkOrderStatus.Open
                    || x.Status == MaintenanceWorkOrderStatus.InProgress))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        string code, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var query = context.Set<MaintenanceWorkOrder>().Where(x => x.Code == code);
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MaintenanceWorkOrder>> ListDoneInWindowAsync(
        Guid machineId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => await context.Set<MaintenanceWorkOrder>()
            .AsNoTracking()
            .Where(x => x.MachineId == machineId
                && x.Status == MaintenanceWorkOrderStatus.Done
                && x.CompletedAt != null
                && x.CompletedAt >= fromUtc
                && x.CompletedAt <= toUtc)
            .OrderBy(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<MaintenanceWorkOrder> AddAsync(
        MaintenanceWorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        context.Set<MaintenanceWorkOrder>().Add(workOrder);
        await context.SaveChangesAsync(cancellationToken);
        return workOrder;
    }

    public async Task UpdateAsync(MaintenanceWorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        context.Set<MaintenanceWorkOrder>().Update(workOrder);
        await context.SaveChangesAsync(cancellationToken);
    }
}
