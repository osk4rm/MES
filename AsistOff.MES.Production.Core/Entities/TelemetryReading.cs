using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// One immutable machine reading appended against a
/// <see cref="MachineTelemetryTag"/>. Append-only: there are no update or
/// delete use cases. Numeric/boolean values use <see cref="DoubleValue"/>;
/// text values use <see cref="StringValue"/> — exactly one of them is set,
/// consistent with the tag <see cref="TelemetryDataType"/>.
/// </summary>
public class TelemetryReading : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public Guid TagId { get; set; }

    /// <summary>Denormalized from the tag for machine-scoped queries.</summary>
    public Guid MachineId { get; set; }

    public DateTime ReadAt { get; set; }

    public double? DoubleValue { get; set; }

    public string? StringValue { get; set; }

    public TelemetryQuality Quality { get; set; } = TelemetryQuality.Good;

    public virtual MachineTelemetryTag? Tag { get; set; }
}
