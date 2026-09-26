using System.Text.Json;
using AsistOff.MES.Multitenancy.Contracts.Events;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;

namespace AsistOff.MES.Multitenancy.Outbox;

/// <summary>
/// Slice 3 (#260) staging contract for the tenant-created event. The handler
/// stages through this abstraction (instead of publishing inline via
/// <c>IEventDispatcher</c>) so the event reaches the relay only as a
/// committed outbox row and a rolled-back tenant save stages nothing.
/// </summary>
public interface ITenantCreatedEventOutbox
{
    /// <summary>
    /// Stages one undispatched <see cref="OutboxMessage"/> for
    /// <paramref name="tenantCreatedEvent"/>. The row's tenant scope comes
    /// from the event's tenant id (the committed tenant row), never from
    /// caller input.
    /// </summary>
    Task StageAsync(TenantCreatedEvent tenantCreatedEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Slice 3 (#260) <see cref="ITenantCreatedEventOutbox"/> backed by the
/// shared outbox table. Called after the tenant row commits, so the relay
/// (slice 2) can only ever read events for committed tenants: ghost events
/// on rollback are impossible by ordering, and a tenant-save failure never
/// reaches this writer, leaving zero undispatched rows for that attempt.
/// Tenant isolation rides on the explicit <c>TenantId</c> (the created tenant
/// id): anonymous signup has no ambient tenant, and the
/// <c>SaasyEntityInterceptor</c> is a no-op for explicitly-scoped inserts, so
/// no manual predicate and no <c>IgnoreQueryFilters</c> bypass are needed.
/// The generic <see cref="OutboxStager"/> secret scan is deliberately not
/// used here: its <c>password</c> fragment would reject the
/// <c>HashedPassword</c> member, but that member is a one-way hash (never
/// plaintext — the handler hashes before staging) and the listener cannot
/// provision the admin login without it. The oversize guard
/// (<see cref="OutboxStager.MaxPayloadLength"/>) is enforced directly.
/// </summary>
public sealed class TenantCreatedEventOutboxWriter(
    DefaultContext context,
    IGuidProvider guidProvider,
    IDateTimeProvider dateTimeProvider) : ITenantCreatedEventOutbox
{
    public async Task StageAsync(TenantCreatedEvent tenantCreatedEvent, CancellationToken cancellationToken = default)
    {
        if (tenantCreatedEvent.Id == Guid.Empty)
        {
            throw new ValidationException(
                nameof(TenantCreatedEvent.Id),
                "Cannot stage a tenant-created event without the committed tenant id.");
        }

        var payload = JsonSerializer.Serialize(tenantCreatedEvent);

        if (payload.Length > OutboxStager.MaxPayloadLength)
        {
            throw new ValidationException(
                nameof(OutboxMessage.Payload),
                $"Tenant-created event payload of {payload.Length} characters exceeds the maximum of {OutboxStager.MaxPayloadLength} characters and cannot be staged to the outbox.");
        }

        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = guidProvider.NewGuid(),
            TenantId = tenantCreatedEvent.Id,
            IdempotencyKey = guidProvider.NewGuid().ToString("N"),
            Type = typeof(TenantCreatedEvent).AssemblyQualifiedName ?? typeof(TenantCreatedEvent).FullName!,
            Payload = payload,
            OccurredOnUtc = dateTimeProvider.UtcNow,
            Dispatched = false,
            RetryCount = 0,
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
