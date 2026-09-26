using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Shared.Infrastructure.Persistence;

/// <summary>
/// EF Core <see cref="IUnitOfWork"/> over the shared <see cref="DefaultContext"/>.
/// All module repositories resolve the same scoped context, so opening one
/// explicit transaction here enlists every <c>SaveChangesAsync</c> issued
/// inside the callback: either all fan-out writes commit together or all
/// roll back together. Retries use the provider execution strategy, with the
/// whole action (including <c>BeginTransactionAsync</c>) inside the strategy
/// as EF Core requires for explicit transactions.
/// </summary>
internal sealed class EfUnitOfWork(DefaultContext context) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<object?>(async ct =>
        {
            await action(ct);
            return null;
        }, cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(_ => action(), cancellationToken);
    }
}
