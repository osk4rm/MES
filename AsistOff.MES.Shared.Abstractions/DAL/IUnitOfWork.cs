namespace AsistOff.MES.Shared.Abstractions.DAL;

/// <summary>
/// Runs a set of repository writes inside a single database transaction so a
/// multi-write fan-out (confirmation plus movements plus genealogy edges plus
/// order status flip) commits atomically. All repositories share the scoped
/// <c>DefaultContext</c>, so opening one transaction on that context enlists
/// every <c>SaveChangesAsync</c> issued inside <paramref name="operation"/>.
/// Validation (404/400/409) runs before the transaction starts, so failed
/// validations write nothing and never open a transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Executes <paramref name="operation"/> inside a transaction and commits.
    /// Any exception rolls the transaction back and propagates to the caller,
    /// leaving no partial rows behind.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Void overload of <see cref="ExecuteInTransactionAsync{T}"/>.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}
