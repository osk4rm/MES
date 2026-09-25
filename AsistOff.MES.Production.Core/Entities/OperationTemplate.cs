using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Production.Domain.Entities;

/// <summary>
/// A reusable template for an <see cref="OperationNode"/>. Templates are maintained
/// in the configuration and can be instantiated when building a recipe version.
/// </summary>
public class OperationTemplate : IEntity, ISaasy, IAuditable
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? OperationType { get; set; }
    public bool IsActive { get; set; } = true;

    // Timing defaults (all optional)
    public decimal? SetupTimeMinutes { get; set; }
    public RunTimeMode RunTimeMode { get; set; } = RunTimeMode.PerUnitSeconds;
    public decimal? RunTimePerUnitSeconds { get; set; }
    public decimal? RunTimePerBatchMinutes { get; set; }
    public decimal? TeardownTimeMinutes { get; set; }
    public decimal? QueueTimeMinutes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
}
