using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class StockMovementsRepository(DefaultContext context) : IStockMovementsRepository
{
    public async Task<IReadOnlyCollection<StockMovement>> ListForConfirmationAsync(
        Guid productionConfirmationId, CancellationToken cancellationToken = default)
        => await context.Set<StockMovement>()
            .AsNoTracking()
            .Where(x => x.ProductionConfirmationId == productionConfirmationId)
            .OrderBy(x => x.MovementType)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(
        IReadOnlyCollection<StockMovement> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
            return;

        context.Set<StockMovement>().AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);
    }
}
