using AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Configuration.Application.Features.WorkCenterCalendars.Get;

public record GetWorkCenterCalendarRequest(Guid MachineId) : ITenantRequest<WorkCenterCalendarResponse>;
