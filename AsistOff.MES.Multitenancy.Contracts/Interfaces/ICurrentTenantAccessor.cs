namespace AsistOff.MES.Multitenancy.Contracts.Interfaces;

/// <summary>
/// Safe accessor for the tenant identifier of the ambient request.
/// Unlike <see cref="ITenantContext"/> this never throws — it returns
/// <see cref="Guid.Empty"/> when no tenant is available (e.g. during startup
/// migrations, background workers, or unauthenticated endpoints).
///
/// Used primarily by infrastructure code (EF Core global query filter,
/// SaaS entity interceptor) which cannot rely on exceptions to signal absence.
/// </summary>
public interface ICurrentTenantAccessor
{
    /// <summary>
    /// Current tenant identifier or <see cref="Guid.Empty"/> when unknown.
    /// </summary>
    Guid CurrentTenantId { get; }

    /// <summary>
    /// Attempts to read the current tenant identifier.
    /// Returns <c>true</c> when a non‑empty tenant id is available.
    /// </summary>
    bool TryGetTenantId(out Guid tenantId);
}
