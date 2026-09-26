using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class ScheduledOperationsRepository(DefaultContext context) : IScheduledOperationsRepository
{
    public async Task<IReadOnlyCollection<ScheduledOperation>> ListForOrdersAsync(
        IReadOnlyCollection<Guid> productionOrderIds, CancellationToken cancellationToken = default)
    {
        if (productionOrderIds.Count == 0)
            return [];

        // Tenant isolation comes from the global query filter; no manual
        // TenantId predicate (AGENT.md).
        return await context.Set<ScheduledOperation>()
            .AsNoTracking()
            .Where(x => productionOrderIds.Contains(x.ProductionOrderId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ScheduledOperation>> ListForMachineAsync(
        Guid machineId, CancellationToken cancellationToken = default)
    {
        // Tenant isolation comes from the global query filter; no manual
        // TenantId predicate (AGENT.md).
        return await context.Set<ScheduledOperation>()
            .AsNoTracking()
            .Where(x => x.MachineId == machineId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScheduledOperation> AddAsync(ScheduledOperation entity, CancellationToken cancellationToken = default)
    {
        context.Set<ScheduledOperation>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(ScheduledOperation entity, CancellationToken cancellationToken = default)
    {
        context.Set<ScheduledOperation>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
