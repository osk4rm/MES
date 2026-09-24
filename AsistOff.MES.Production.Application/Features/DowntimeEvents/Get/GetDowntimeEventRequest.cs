using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Get;

public record GetDowntimeEventRequest(Guid Id) : ITenantRequest<DowntimeEventResponse>;
