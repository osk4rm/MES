using System.ComponentModel.DataAnnotations.Schema;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// A controlled-vocabulary code attached to a downtime event or scrap
/// confirmation (e.g. <c>DT-MAINT-BREAKDOWN</c>, <c>SCRAP-TOLERANCE-OVER</c>).
/// Enables Pareto analysis of downtime and quality losses.
/// </summary>
public class ReasonCode : IEntity, ISaasy, IHasDomainEvents
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ReasonCodeCategory Category { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortIndex { get; set; }

    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Transient domain events staged to the transactional outbox on save.
    /// Excluded from the EF model: the outbox write path (slice 1, #258)
    /// needs at least one tenant-scoped producer, and no handler raises
    /// events yet, so application behavior is unchanged.
    /// </summary>
    [NotMapped]
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
