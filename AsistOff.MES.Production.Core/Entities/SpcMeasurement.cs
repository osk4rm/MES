using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A single measured value recorded against an <see cref="SpcCharacteristic"/>.
/// Append-only: there is no update path; corrections are delete plus re-create.
/// Out-of-control evaluation (Western Electric rule 1) is computed on read
/// against the characteristic control limits.
/// </summary>
public class SpcMeasurement : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Characteristic this measurement was taken against (same tenant, FK with cascade delete).</summary>
    public Guid CharacteristicId { get; set; }

    public decimal Value { get; set; }

    /// <summary>UTC timestamp of when the measurement was taken (immutable after create).</summary>
    public DateTime MeasuredAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual SpcCharacteristic? Characteristic { get; set; }
}
