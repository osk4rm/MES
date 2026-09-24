using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// One recurring weekly window of a <see cref="WorkCenterCalendar"/>.
/// The interval is half-open — <c>[StartTime, EndTime)</c> — and when
/// <see cref="EndTime"/> is not after <see cref="StartTime"/> it crosses
/// midnight into the following day.
/// </summary>
public class WorkCenterCalendarEntry : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WorkCenterCalendarId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>Optional link to the named <see cref="Shift"/> this window belongs to.</summary>
    public Guid? ShiftId { get; set; }

    /// <summary>
    /// <c>true</c> for planned production time, <c>false</c> for a planned
    /// non-working window (break, maintenance).
    /// </summary>
    public bool IsWorking { get; set; } = true;

    public virtual WorkCenterCalendar? WorkCenterCalendar { get; set; }
}
