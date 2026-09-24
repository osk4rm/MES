using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// Assigns an <see cref="Operator"/> to a <see cref="Shift"/> on a given
/// calendar date. The minimal tenant-scoped roster used to attribute future
/// confirmations and downtime to who was on shift.
/// </summary>
public class OperatorShiftAssignment : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OperatorId { get; set; }
    public Guid ShiftId { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }

    public virtual Operator? Operator { get; set; }
    public virtual Shift? Shift { get; set; }
}
