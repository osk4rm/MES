using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Delete;

public record DeleteDowntimeEventRequest(Guid Id) : ITenantRequest;
