namespace AsistOff.MES.Shared.Abstractions.DAL;

/// <summary>
/// Ambient EF Core transaction boundary shared by all repositories backed by
/// <c>DefaultContext</c> (single scoped <c>DbContext</c>). Use it when one
/// use case fans out to several repository writes that must commit or roll
/// back together (e.g. operator confirmation: confirmation row plus RW/PW
/// movements plus genealogy edges plus order status flip).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="action"/> inside a single database transaction
    /// (with the provider execution strategy for transient retries).
    /// Commits when <paramref name="action"/> completes; rolls back and
    /// rethrows when it faults. Validations must run before calling this so
    /// failed validations never open a transaction and write nothing.
    /// </summary>
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="ExecuteAsync(Func{CancellationToken, Task}, CancellationToken)"/>
    /// but returns the value produced inside the transaction.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
