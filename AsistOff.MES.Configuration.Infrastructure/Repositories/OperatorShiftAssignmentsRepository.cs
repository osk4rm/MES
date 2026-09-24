using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

internal sealed class OperatorShiftAssignmentsRepository(DefaultContext context)
    : IOperatorShiftAssignmentsRepository
{
    public async Task<IReadOnlyCollection<OperatorShiftAssignment>> BrowseAsync(
        Paginator<OperatorShiftAssignment> paginator, CancellationToken cancellationToken = default)
    {
        return await context.Set<OperatorShiftAssignment>()
            .Include(x => x.Operator)
            .Include(x => x.Shift)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        ExpressionStarter<OperatorShiftAssignment> predicate, CancellationToken cancellationToken = default)
    {
        return await context.Set<OperatorShiftAssignment>().Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<OperatorShiftAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<OperatorShiftAssignment>()
            .Include(x => x.Operator)
            .Include(x => x.Shift)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid operatorId, Guid shiftId, DateOnly date, CancellationToken cancellationToken = default)
    {
        return await context.Set<OperatorShiftAssignment>()
            .AnyAsync(x => x.OperatorId == operatorId && x.ShiftId == shiftId && x.Date == date, cancellationToken);
    }

    public async Task<OperatorShiftAssignment> AddAsync(
        OperatorShiftAssignment assignment, CancellationToken cancellationToken = default)
    {
        context.Set<OperatorShiftAssignment>().Add(assignment);
        await context.SaveChangesAsync(cancellationToken);
        return assignment;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Set<OperatorShiftAssignment>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
