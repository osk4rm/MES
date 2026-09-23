using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// A controlled-vocabulary code attached to a downtime event or scrap
/// confirmation (e.g. <c>DT-MAINT-BREAKDOWN</c>, <c>SCRAP-TOLERANCE-OVER</c>).
/// Enables Pareto analysis of downtime and quality losses.
/// </summary>
public class ReasonCode : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ReasonCodeCategory Category { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortIndex { get; set; }
}
