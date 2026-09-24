using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Delete;

public record DeleteScrapEventRequest(Guid Id) : ITenantRequest;
