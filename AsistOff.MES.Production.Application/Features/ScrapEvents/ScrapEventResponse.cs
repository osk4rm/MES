namespace AsistOff.MES.Production.Application.Features.ScrapEvents;

public record ScrapEventResponse(
    Guid Id,
    Guid MachineId,
    Guid ReasonCodeId,
    decimal Quantity,
    DateTime ReportedAt,
    string? Notes,
    Guid? ReportedByOperatorId,
    Guid? ProductionOrderId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
