using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class MachinesRepository(DefaultContext context) : IMachinesRepository
{
    public async Task<IReadOnlyCollection<Machine>> BrowseAsync(Paginator<Machine> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<Machine>()
            .Include(x => x.Department)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(ExpressionStarter<Machine> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<Machine>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<Machine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<Machine>()
            .Include(x => x.Department)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Machine> AddAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        context.Set<Machine>().Add(machine);
        await context.SaveChangesAsync(cancellationToken);
        return machine;
    }

    public async Task UpdateAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        context.Set<Machine>().Update(machine);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Tracked remove (not ExecuteDeleteAsync) so the AuditHistoryInterceptor
        // observes the delete and appends the history row in the same transaction.
        var machine = await context.Set<Machine>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (machine is null)
        {
            return;
        }

        context.Set<Machine>().Remove(machine);
        await context.SaveChangesAsync(cancellationToken);
    }
}
