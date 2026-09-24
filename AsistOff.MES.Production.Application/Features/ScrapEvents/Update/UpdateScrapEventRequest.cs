using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Update;

public record UpdateScrapEventRequest(
    Guid Id,
    Guid ReasonCodeId,
    decimal Quantity,
    string? Notes) : ITenantRequest;
