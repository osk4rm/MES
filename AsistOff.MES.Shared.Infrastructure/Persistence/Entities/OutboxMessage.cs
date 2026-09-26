using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Shared.Infrastructure.Persistence.Entities;

/// <summary>
/// Durable transactional-outbox row. Slice 1 (#258) of the outbox series owns
/// the write path: <c>PublishDomainEventsInterceptor</c> stages one row per
/// domain event inside the same <c>DbContext</c> transaction as the primary
/// write, so a rolled-back write leaves no staged rows behind.
///
/// Row lifecycle:
/// <list type="number">
/// <item>Created with <c>Dispatched = false</c> and <c>RetryCount = 0</c>,
/// sharing the source entity's <c>TenantId</c> and transaction.</item>
/// <item>The slice-2 relay (<c>OutboxRelayService</c>) fetches undispatched
/// rows oldest-first via <c>OutboxStager.ApplyUndispatched</c>, publishes
/// them through MediatR after commit, then marks them dispatched
/// (incrementing <c>RetryCount</c> on transient failures with exponential
/// backoff).</item>
/// <item>A row whose <c>RetryCount</c> reaches the <c>Outbox:MaxAttempts</c>
/// budget — or that can never be delivered (oversized payload, unknown event
/// type, corrupt payload) — is parked as poison: <c>RetryCount</c> is pinned
/// to the budget so the relay query never refetches it. No new columns were
/// added for poison/dispatch timestamps (slice 2 owns no migration), so
/// <c>Dispatched = true</c> always means "delivered", never "parked".</item>
/// </list>
/// Nothing is published before the staging transaction commits, so a
/// rolled-back write leaves no staged rows and produces zero dispatches.
/// </summary>
public class OutboxMessage : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>
    /// Unique idempotency key (one GUID per staged event) so the slice-2
    /// relay can deliver at-least-once without double-processing.
    /// </summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>Assembly-qualified CLR type name of the staged domain event.</summary>
    public required string Type { get; set; }

    /// <summary>
    /// JSON snapshot of the domain event. Never contains secrets: staging
    /// rejects events carrying password/secret/token-like members and
    /// rejects oversized payloads (see <c>OutboxStager</c>).
    /// </summary>
    public required string Payload { get; set; }

    public DateTime OccurredOnUtc { get; set; }
    public bool Dispatched { get; set; }
    public int RetryCount { get; set; }
}
