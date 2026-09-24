using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// A corrective maintenance work order raised against a <see cref="Machine"/>
/// (Work Center). Lightweight CMMS slice: tracks Open → InProgress → Done
/// or Cancelled plus a basic failure and resolution history.
/// </summary>
public class MaintenanceWorkOrder : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public Guid MachineId { get; set; }
    public MaintenanceWorkOrderPriority Priority { get; set; }
    public MaintenanceWorkOrderStatus Status { get; set; } = MaintenanceWorkOrderStatus.Open;
    public DateTime ReportedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ResolutionNotes { get; set; }

    public virtual Machine? Machine { get; set; }
}
