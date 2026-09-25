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
/// <item>Slice 2 relay (planned): fetches undispatched rows oldest-first via
/// <c>OutboxStager.ApplyUndispatched</c>, delivers them, then marks them
/// dispatched (incrementing <c>RetryCount</c> on transient failures).</item>
/// <item>Slice 3 cutover (planned): direct pre-commit MediatR delivery is
/// removed, so the outbox becomes the only cross-module path and ghost
/// events on rollback disappear entirely.</item>
/// </list>
/// Until slice 3, the existing pre-commit MediatR delivery is kept unchanged
/// alongside staging.
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
