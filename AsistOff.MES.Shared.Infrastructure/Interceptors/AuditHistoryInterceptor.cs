using System.Text.Json;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Audit;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AsistOff.MES.Shared.Infrastructure.Interceptors;

/// <summary>
/// Appends one <see cref="AuditEvent"/> row per create, update and delete of
/// the pilot entities (Production Order, Machine, Production Confirmation —
/// see <see cref="AuditEntityNames"/>).
///
/// The rows are added to the same <see cref="DbContext"/> inside
/// <c>SavingChanges</c>, so they commit in the same transaction as the primary
/// write: a failed primary write rolls back its history rows too and no ghost
/// history can exist. The interceptor is deliberately generic (pilot detection
/// by CLR type name, values read from the EF change tracker) so this
/// low-level module never references the Production/Configuration domain
/// assemblies.
///
/// History rows are append-only: any attempt to update or delete an
/// <see cref="AuditEvent"/> through this context throws.
/// </summary>
public class AuditHistoryInterceptor(
    IDateTimeProvider dateTimeProvider,
    ICurrentUserAccessor currentUserAccessor,
    ICurrentTenantAccessor tenantAccessor,
    IGuidProvider guidProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AppendHistory(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendHistory(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendHistory(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var auditEntry in context.ChangeTracker.Entries<AuditEvent>())
        {
            if (auditEntry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "AuditEvents are append-only and cannot be updated or deleted.");
            }
        }

        var pending = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && e.Entity is not AuditEvent
                && AuditEntityNames.IsPilot(e.Metadata.ClrType.Name))
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        var changedAt = dateTimeProvider.UtcNow;
        var actor = currentUserAccessor.UserId;

        foreach (var entry in pending)
        {
            var deleted = entry.State == EntityState.Deleted;
            var entityId = ReadGuid(entry, "Id", deleted);
            if (entityId == Guid.Empty)
            {
                continue;
            }

            var tenantId = ReadGuid(entry, "TenantId", deleted);
            if (tenantId == Guid.Empty && tenantAccessor.TryGetTenantId(out var ambient))
            {
                tenantId = ambient;
            }

            context.Add(new AuditEvent
            {
                Id = guidProvider.NewGuid(),
                TenantId = tenantId,
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                Action = entry.State switch
                {
                    EntityState.Added => AuditEventAction.Created,
                    EntityState.Modified => AuditEventAction.Updated,
                    EntityState.Deleted => AuditEventAction.Deleted,
                    _ => throw new InvalidOperationException(
                        $"Unexpected entity state '{entry.State}' while building audit history."),
                },
                ChangedAt = changedAt,
                ActorId = actor,
                Payload = BuildPayload(entry, deleted),
            });
        }
    }

    private static Guid ReadGuid(EntityEntry entry, string propertyName, bool useOriginal)
    {
        PropertyEntry property;
        try
        {
            property = entry.Property(propertyName);
        }
        catch (InvalidOperationException)
        {
            return Guid.Empty;
        }

        var value = useOriginal ? property.OriginalValue : property.CurrentValue;
        return value switch
        {
            Guid id => id,
            _ => Guid.Empty,
        };
    }

    /// <summary>
    /// Serializes the scalar property snapshot of the write. Informational
    /// only: a serialization failure degrades to a null payload rather than
    /// blocking the primary write.
    /// </summary>
    private static string? BuildPayload(EntityEntry entry, bool useOriginal)
    {
        try
        {
            var snapshot = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsShadowProperty())
                {
                    continue;
                }

                snapshot[property.Metadata.Name] = useOriginal ? property.OriginalValue : property.CurrentValue;
            }

            return JsonSerializer.Serialize(snapshot);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
