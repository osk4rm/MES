using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendar.Get;

public record GetWorkCenterCalendarRequest(Guid MachineId) : ITenantRequest<WorkCenterCalendarResponse>;
