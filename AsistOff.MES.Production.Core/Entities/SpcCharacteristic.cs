using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A quality characteristic monitored with statistical process control
/// (e.g. shaft diameter, fill weight, defect rate). Defines the
/// specification limits, control limits and chart type that future
/// measurement capture and out-of-control detection attach to.
/// </summary>
public class SpcCharacteristic : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Loose reference to the measured product. Production does not
    /// reference the Configuration module; existence validation is out of scope.
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// Loose reference to the Work Center where the characteristic is measured.
    /// </summary>
    public Guid? MachineId { get; set; }

    public SpcChartType ChartType { get; set; } = SpcChartType.XbarR;
    public decimal? NominalValue { get; set; }
    public decimal? LowerSpecLimit { get; set; }
    public decimal? UpperSpecLimit { get; set; }
    public decimal? LowerControlLimit { get; set; }
    public decimal? UpperControlLimit { get; set; }
    public int SampleSize { get; set; } = 5;
    public string? Unit { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
