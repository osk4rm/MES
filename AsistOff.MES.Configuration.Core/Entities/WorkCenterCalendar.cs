using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// Weekly availability calendar of a single Work Center (<see cref="Machine"/>).
/// At most one calendar exists per <c>(TenantId, MachineId)</c>. The recurring
/// weekly schedule lives in <see cref="Entries"/>.
/// </summary>
public class WorkCenterCalendar : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid MachineId { get; set; }

    public virtual Machine? Machine { get; set; }
    public virtual ICollection<WorkCenterCalendarEntry> Entries { get; set; } = new List<WorkCenterCalendarEntry>();
}
