using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AsistOff.MES.Shared.Infrastructure.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that enforces tenant boundary on all
/// entities implementing <see cref="ISaasy"/>.
///
/// Rules:
/// * On <see cref="EntityState.Added"/> — when <c>TenantId</c> is empty, it is
///   auto‑assigned from the ambient <see cref="ICurrentTenantAccessor"/>.
///   When explicitly set to a different tenant a <see cref="InvalidOperationException"/> is thrown.
/// * On <see cref="EntityState.Modified"/> — any change to <c>TenantId</c> is
///   rejected (entities cannot be re‑assigned across tenants via update).
/// * When there is no ambient tenant (e.g. startup migration run), the
///   interceptor is a no‑op: callers that are not HTTP‑bound must set
///   <c>TenantId</c> explicitly (and are responsible for correctness).
/// </summary>
public sealed class SaasyEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public SaasyEntityInterceptor(ICurrentTenantAccessor tenantAccessor)
    {
        _tenantAccessor = tenantAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        EnforceTenant(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnforceTenant(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnforceTenant(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var hasTenant = _tenantAccessor.TryGetTenantId(out var currentTenantId);

        foreach (var entry in context.ChangeTracker.Entries<ISaasy>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.TenantId == Guid.Empty)
                    {
                        if (!hasTenant)
                        {
                            throw new InvalidOperationException(
                                $"Cannot insert tenant‑scoped entity '{entry.Entity.GetType().Name}' without an ambient tenant context.");
                        }
                        entry.Entity.TenantId = currentTenantId;
                    }
                    else if (hasTenant && entry.Entity.TenantId != currentTenantId)
                    {
                        throw new InvalidOperationException(
                            $"Cross‑tenant write detected for '{entry.Entity.GetType().Name}' (ambient={currentTenantId}, entity={entry.Entity.TenantId}).");
                    }
                    break;

                case EntityState.Modified:
                    var tenantProperty = entry.Property(nameof(ISaasy.TenantId));
                    if (tenantProperty.IsModified && !Equals(tenantProperty.OriginalValue, tenantProperty.CurrentValue))
                    {
                        throw new InvalidOperationException(
                            $"TenantId of '{entry.Entity.GetType().Name}' cannot be changed after insert.");
                    }
                    break;
            }
        }
    }
}
