using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Get;

public record GetScrapEventRequest(Guid Id) : ITenantRequest<ScrapEventResponse>;
