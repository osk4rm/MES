namespace AsistOff.MES.Shared.Abstractions.DAL;

/// <summary>
/// Executes a set of repository writes inside a single database transaction.
/// All repositories share the scoped <c>DefaultContext</c>, so individual
/// <c>SaveChangesAsync</c> calls enlist in the ambient transaction and a
/// failure rolls back every write together.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="action"/> inside a transaction, committing on
    /// success and rolling back when <paramref name="action"/> throws.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}
