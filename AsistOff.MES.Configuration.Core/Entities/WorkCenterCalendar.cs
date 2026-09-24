using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// The weekly availability pattern of a single Work Center (<see cref="Machine"/>).
/// At most one calendar exists per <c>(TenantId, MachineId)</c>; its planned
/// production time is the sum of the working <see cref="Entries"/> and is the
/// denominator of the OEE <c>Availability</c> ratio.
/// </summary>
public class WorkCenterCalendar : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid MachineId { get; set; }

    public virtual ICollection<WorkCenterCalendarEntry> Entries { get; set; } =
        new List<WorkCenterCalendarEntry>();
}
