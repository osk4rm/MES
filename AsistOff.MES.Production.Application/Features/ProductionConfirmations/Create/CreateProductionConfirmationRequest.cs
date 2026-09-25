using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;

public record ConsumedLotEntry(
    Guid LotId,
    decimal Quantity);

[RequirePermission(RbacDefaults.ProductionWrite)]
public record CreateProductionConfirmationRequest(
    Guid ProductionOrderId,
    Guid MachineId,
    Guid? ReportedByOperatorId,
    DateTime ReportedAt,
    decimal GoodQuantity,
    decimal ScrapQuantity,
    string? Notes,
    Guid? ProducedLotId = null,
    IReadOnlyCollection<ConsumedLotEntry>? ConsumedLots = null) : ITenantRequest<ProductionConfirmationResponse>;
