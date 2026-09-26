namespace AsistOff.MES.Production.Domain.Repositories;

/// <summary>
/// Transaction boundary for multi-write Production flows. All repositories
/// share the scoped <c>DefaultContext</c>, so opening one database
/// transaction here makes every <c>SaveChangesAsync</c> inside
/// <paramref name="operation"/> commit or roll back together.
/// </summary>
public interface IProductionUnitOfWork
{
    /// <summary>
    /// Executes <paramref name="operation"/> inside a single database
    /// transaction. Commits when the operation succeeds; rolls back and
    /// rethrows when it fails.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-generic variant for write-only flows.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}
