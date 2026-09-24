namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations;

public record ProductionConfirmationResponse(
    Guid Id,
    Guid ProductionOrderId,
    Guid MachineId,
    Guid? ReportedByOperatorId,
    DateTime ReportedAt,
    decimal GoodQuantity,
    decimal ScrapQuantity,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
