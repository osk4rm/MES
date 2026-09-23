using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;

namespace AsistOff.MES.Configuration.Domain.Entities;

/// <summary>
/// One recurring weekly availability window of a <see cref="WorkCenterCalendar"/>.
/// An entry with <c>EndTime &lt;= StartTime</c> crosses midnight into the next day.
/// </summary>
public class WorkCenterCalendarEntry : IEntity, ISaasy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CalendarId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid? ShiftId { get; set; }
    public bool IsWorking { get; set; } = true;

    public virtual WorkCenterCalendar? Calendar { get; set; }
    public virtual Shift? Shift { get; set; }
}
