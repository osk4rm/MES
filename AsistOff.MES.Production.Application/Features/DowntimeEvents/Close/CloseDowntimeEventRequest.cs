using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Close;

public record CloseDowntimeEventRequest(
    Guid Id,
    DateTime? EndedAt) : ITenantRequest<DowntimeEventResponse>;
