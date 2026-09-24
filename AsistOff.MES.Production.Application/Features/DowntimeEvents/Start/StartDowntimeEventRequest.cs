using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Start;

public record StartDowntimeEventRequest(
    Guid MachineId,
    Guid ReasonCodeId,
    DateTime StartedAt,
    string? Notes,
    Guid? ReportedByOperatorId) : ITenantRequest<DowntimeEventResponse>;
