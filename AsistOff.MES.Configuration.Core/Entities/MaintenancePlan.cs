using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// A preventive maintenance schedule owned by a <see cref="Machine"/>
/// (Work Center). Slice 1/3 of preventive CMMS: the plan registry only.
/// A <see cref="MaintenancePlanTriggerType.Time"/> plan recurs every
/// <see cref="IntervalDays"/> days and is due at <see cref="NextDueAt"/>;
/// a <see cref="MaintenancePlanTriggerType.Meter"/> plan recurs every
/// <see cref="MeterIntervalValue"/> meter units. Due evaluation and
/// automatic work-order creation arrive in slice 2/3.
/// </summary>
public class MaintenancePlan : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid MachineId { get; set; }
    public MaintenancePlanTriggerType TriggerType { get; set; }
    public int? IntervalDays { get; set; }
    public decimal? MeterIntervalValue { get; set; }
    public DateTime? NextDueAt { get; set; }
    public DateTime? LastCompletedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Machine? Machine { get; set; }
}
