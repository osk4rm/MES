using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Record;

public record RecordLotGenealogyEdgeRequest(
    Guid ConsumedLotId,
    Guid ProducedLotId,
    Guid ProductionOrderId,
    Guid? ProductionConfirmationId,
    Guid MachineId,
    Guid? ReportedByOperatorId,
    decimal ConsumedQuantity,
    DateTime OccurredAt,
    string? Notes) : ITenantRequest<LotGenealogyEdgeResponse>;
