using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy.Record;

[RequirePermission(RbacDefaults.ProductionWrite)]
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
