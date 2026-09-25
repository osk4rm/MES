using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Shared.Infrastructure.Persistence;

/// <summary>
/// EF Core <see cref="IUnitOfWork"/> over the shared <see cref="DefaultContext"/>.
/// Opens one <c>BeginTransactionAsync</c> scope, runs the callback (whose
/// repository <c>SaveChangesAsync</c> calls enlist in the ambient transaction),
/// then commits; any exception rolls the transaction back and rethrows so no
/// partial fan-out survives.
/// </summary>
public sealed class EfUnitOfWork(DefaultContext context) : IUnitOfWork
{
    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await action();

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
