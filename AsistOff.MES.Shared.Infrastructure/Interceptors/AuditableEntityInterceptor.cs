using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AsistOff.MES.Shared.Infrastructure.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditable"/> audit columns on save. The actor
/// (<c>CreatedBy</c>/<c>ModifiedBy</c>) is resolved from
/// <see cref="ICurrentUserAccessor"/>; anonymous bootstrap writes
/// (sign-in, tenant provisioning) have no authenticated caller and
/// persist null actors. Registered as scoped so the scoped accessor
/// can be read safely.
/// </summary>
public class AuditableEntityInterceptor(
    IDateTimeProvider dateTimeProvider,
    ICurrentUserAccessor currentUserAccessor) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context == null)
        {
            return;
        }

        var actor = currentUserAccessor.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(x => x.CreatedAt).CurrentValue = dateTimeProvider.UtcNow;
                    entry.Property(x => x.CreatedBy).CurrentValue = actor;
                    entry.Property(x => x.ModifiedBy).CurrentValue = null;
                    break;
                case EntityState.Modified:
                    entry.Property(x => x.UpdatedAt).CurrentValue = dateTimeProvider.UtcNow;
                    entry.Property(x => x.ModifiedBy).CurrentValue = actor;
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Property(x => x.CreatedBy).IsModified = false;
                    break;
            }
        }
    }
}