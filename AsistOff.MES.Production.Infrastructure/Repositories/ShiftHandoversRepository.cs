using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Production.Infrastructure.Repositories;

internal sealed class ShiftHandoversRepository(DefaultContext context) : IShiftHandoversRepository
{
    public async Task<IReadOnlyCollection<ShiftHandover>> BrowseAsync(Paginator<ShiftHandover> paginator, CancellationToken cancellationToken = default)
        => await context.Set<ShiftHandover>()
            .AsNoTracking()
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(ExpressionStarter<ShiftHandover> predicate, CancellationToken cancellationToken = default)
        => context.Set<ShiftHandover>().Where(predicate).CountAsync(cancellationToken);

    public Task<ShiftHandover?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<ShiftHandover>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<ShiftHandover> AddAsync(ShiftHandover entity, CancellationToken cancellationToken = default)
    {
        context.Set<ShiftHandover>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task<bool> ExistsAsync(Guid machineId, DateTime fromUtc, CancellationToken cancellationToken = default)
        => context.Set<ShiftHandover>()
            .AnyAsync(x => x.MachineId == machineId && x.From == fromUtc, cancellationToken);
}
