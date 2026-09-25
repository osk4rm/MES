using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Save;

/// <summary>One weekly window sent by the client when replacing a calendar.</summary>
public record WorkCenterCalendarEntryRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid? ShiftId,
    bool IsWorking);

/// <summary>
/// Create-or-replace command for the weekly calendar of a Work Center.
/// The route carries the machine id; the body carries only the entries.
/// </summary>
[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record SaveWorkCenterCalendarRequest(
    Guid MachineId,
    IReadOnlyCollection<WorkCenterCalendarEntryRequest> Entries)
    : ITenantRequest<WorkCenterCalendarResponse>;
