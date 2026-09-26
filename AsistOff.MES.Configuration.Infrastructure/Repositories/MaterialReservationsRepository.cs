using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class MaterialReservationsRepository(DefaultContext context) : IMaterialReservationsRepository
{
    public Task<MaterialReservation?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<MaterialReservation>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<MaterialReservation>> ListForOrderAsync(
        Guid productionOrderId, CancellationToken cancellationToken = default)
        => await context.Set<MaterialReservation>()
            .Where(x => x.ProductionOrderId == productionOrderId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<MaterialReservation>> ListOpenAsync(
        CancellationToken cancellationToken = default)
        => await context.Set<MaterialReservation>()
            .AsNoTracking()
            .Where(x => x.Status != ReservationStatus.Closed)
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.WarehouseId)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<MaterialReservation>> BrowseAsync(
        Paginator<MaterialReservation> paginator, CancellationToken cancellationToken = default)
        => await context.Set<MaterialReservation>()
            .AsNoTracking()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(
        ExpressionStarter<MaterialReservation> predicate, CancellationToken cancellationToken = default)
        => context.Set<MaterialReservation>().Where(predicate).CountAsync(cancellationToken);

    public async Task AddRangeAsync(
        IReadOnlyCollection<MaterialReservation> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
            return;

        context.Set<MaterialReservation>().AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MaterialReservation entity, CancellationToken cancellationToken = default)
    {
        context.Set<MaterialReservation>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
