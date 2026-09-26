using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Shared.Infrastructure.Persistence;

/// <summary>
/// <see cref="IUnitOfWork"/> backed by the shared <see cref="DefaultContext"/>.
/// Opens one <c>BeginTransactionAsync</c> scope; every repository shares the
/// same scoped context instance, so each <c>SaveChangesAsync</c> inside the
/// delegate enlists in that transaction. Commit happens only after the whole
/// delegate succeeds; any exception disposes the transaction uncommitted
/// (rollback) and propagates.
/// </summary>
internal sealed class DefaultContextUnitOfWork(DefaultContext context) : IUnitOfWork
{
    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        // If the caller already runs inside a transaction (nested fan-out),
        // reuse it instead of starting a savepoint-less nested transaction.
        if (context.Database.CurrentTransaction is not null)
            return await operation();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            await operation();
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await operation();
        await transaction.CommitAsync(cancellationToken);
    }
}
