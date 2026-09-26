using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.EntityFrameworkCore.Storage;

namespace AsistOff.MES.Shared.Infrastructure.Persistence;

/// <summary>
/// EF Core <see cref="IUnitOfWork"/> over the shared <see cref="DefaultContext"/>.
/// All module repositories share the same scoped context instance, so a
/// transaction started here covers every <c>SaveChangesAsync</c> issued by
/// those repositories until commit or rollback.
/// </summary>
internal sealed class DefaultContextUnitOfWork(DefaultContext context) : IUnitOfWork
{
    public async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new DefaultContextTransaction(transaction);
    }

    private sealed class DefaultContextTransaction(IDbContextTransaction transaction) : IDatabaseTransaction
    {
        private bool _completed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await transaction.RollbackAsync(cancellationToken);
            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Best effort: the underlying connection may already be
                    // disposed when the scope tears down after a failure.
                }
            }

            await transaction.DisposeAsync();
        }
    }
}
