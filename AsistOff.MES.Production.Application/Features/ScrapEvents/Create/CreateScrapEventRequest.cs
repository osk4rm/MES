using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents.Create;

public record CreateScrapEventRequest(
    Guid MachineId,
    Guid ReasonCodeId,
    decimal Quantity,
    DateTime ReportedAt,
    string? Notes,
    Guid? ReportedByOperatorId,
    Guid? ProductionOrderId) : ITenantRequest<ScrapEventResponse>;
