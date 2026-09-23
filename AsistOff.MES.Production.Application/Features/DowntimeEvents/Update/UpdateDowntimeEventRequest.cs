using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Update;

public record UpdateDowntimeEventRequest(
    Guid Id,
    Guid ReasonCodeId,
    string? Notes) : ITenantRequest;
