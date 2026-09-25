using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Shared.Infrastructure.Persistence.Entities;

/// <summary>
/// Append-only audit history row. One row is appended per create, update and
/// delete of a pilot entity (Production Order, Machine, Production
/// Confirmation) by the slice-2 write path. Application code never updates
/// or deletes history rows. Deliberately not <see cref="IAuditable"/> so the
/// auditable interceptor never stamps history rows themselves.
/// </summary>
public enum AuditEventAction : short
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
}

/// <summary>
/// Tenant-scoped append-only audit event. Schema is owned by slice 1 (#245)
/// so slice 2 (#246) needs no new migration.
/// </summary>
public class AuditEvent : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string EntityName { get; set; }
    public Guid EntityId { get; set; }
    public AuditEventAction Action { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid? ActorId { get; set; }
    public string? Payload { get; set; }
}
