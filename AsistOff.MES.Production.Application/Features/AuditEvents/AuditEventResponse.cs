namespace AsistOff.MES.Production.Application.Features.AuditEvents;

/// <summary>
/// Read model for one append-only audit history row. The underlying row is
/// never updated or deleted by application code (slice 2/2, issue #246).
/// <c>Payload</c> carries the full change JSON only for callers holding
/// <c>production.write</c>; read-only callers receive <c>null</c> (issue
/// #372) — the event itself (actor, action, entity, time) stays visible.
/// </summary>
public record AuditEventResponse(
    Guid Id,
    string EntityName,
    Guid EntityId,
    short Action,
    DateTime ChangedAt,
    Guid? ActorId,
    string? Payload);
