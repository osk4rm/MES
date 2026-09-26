namespace AsistOff.MES.Shared.Abstractions.DAL;

/// <summary>
/// Ambient database transaction handle. Commits on <see cref="CommitAsync"/>
/// and rolls back on <see cref="RollbackAsync"/> (or on dispose without
/// commit). Implementations wrap the shared <c>DefaultContext</c> transaction
/// so several repository <c>SaveChangesAsync</c> calls become atomic.
/// </summary>
public interface IDatabaseTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Starts database transactions over the shared EF Core context. Lives in
/// abstractions so application handlers can scope multi-write fan-outs
/// (confirmation plus RW/PW movements plus genealogy edges plus order status)
/// without referencing EF Core directly.
/// </summary>
public interface IUnitOfWork
{
    Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
