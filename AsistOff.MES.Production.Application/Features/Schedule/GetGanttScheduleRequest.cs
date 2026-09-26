using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.Schedule;

/// <summary>
/// Read-only time-phased Gantt schedule over a caller-supplied date window.
/// Each Released/InProgress order explodes into operation segments (see
/// <see cref="GanttScheduler"/>) grouped per Work Center. Computed from
/// existing tables plus <c>ScheduledOperation</c> manual overrides; the
/// window is capped at 31 days and 200 order rows like the dispatch board.
/// </summary>
public record GetGanttScheduleRequest(DateOnly From, DateOnly To, Guid? MachineId) : ITenantRequest<GanttScheduleResponse>;
