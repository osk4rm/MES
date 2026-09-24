namespace AsistOff.MES.Multitenancy.Contracts.Interfaces;

/// <summary>
/// Ambient tenant override for background (non-HTTP) work such as hosted
/// services. The EF Core global query filter and the <c>TenantContext</c>
/// read this before falling back to the HTTP request claims, so a background
/// worker can iterate tenants one scope at a time without ever calling
/// <c>IgnoreQueryFilters</c>.
///
/// Values flow with <see cref="AsyncLocal{T}"/>, i.e. they are visible to the
/// current async flow and its children only. Always dispose the returned scope
/// (prefer <c>using</c>) so the previous value is restored.
/// </summary>
public static class BackgroundTenantContext
{
    private static readonly AsyncLocal<Guid?> CurrentStorage = new();

    /// <summary>Ambient background tenant, or <c>null</c> when not in a background scope.</summary>
    public static Guid? Current => CurrentStorage.Value;

    /// <summary>
    /// Begins a background tenant scope. The previous value (if any) is
    /// restored when the scope is disposed, so scopes may nest.
    /// </summary>
    public static IDisposable BeginScope(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));

        var previous = CurrentStorage.Value;
        CurrentStorage.Value = tenantId;
        return new Scope(previous);
    }

    private sealed class Scope(Guid? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CurrentStorage.Value = previous;
        }
    }
}
