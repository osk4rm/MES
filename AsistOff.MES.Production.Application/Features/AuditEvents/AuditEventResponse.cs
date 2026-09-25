namespace AsistOff.MES.Production.Application.Features.AuditEvents;

/// <summary>
/// Read model for one append-only audit history row. The underlying row is
/// never updated or deleted by application code (slice 2/2, issue #246).
/// </summary>
public record AuditEventResponse(
    Guid Id,
    string EntityName,
    Guid EntityId,
    short Action,
    DateTime ChangedAt,
    Guid? ActorId,
    string? Payload);
