namespace AsistOff.MES.Production.Application.Features.LotGenealogy;

public record LotGenealogyEdgeResponse(
    Guid Id,
    Guid ConsumedLotId,
    Guid ProducedLotId,
    Guid ProductionOrderId,
    Guid? ProductionConfirmationId,
    Guid MachineId,
    Guid? ReportedByOperatorId,
    decimal ConsumedQuantity,
    DateTime OccurredAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
