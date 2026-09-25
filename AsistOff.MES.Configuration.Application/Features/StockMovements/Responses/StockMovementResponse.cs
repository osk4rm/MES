namespace AsistOff.MES.Configuration.Application.Features.StockMovements.Responses;

public sealed record StockMovementResponse(
    Guid Id,
    string MovementType,
    Guid ProductId,
    decimal Quantity,
    Guid? MeasureUnitId,
    Guid? WarehouseId,
    Guid ProductionConfirmationId,
    Guid ProductionOrderId,
    DateTime ReportedAt);
