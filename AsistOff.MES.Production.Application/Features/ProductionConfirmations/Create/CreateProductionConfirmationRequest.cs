using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Create;

public record CreateProductionConfirmationRequest(
    Guid ProductionOrderId,
    Guid MachineId,
    Guid? ReportedByOperatorId,
    DateTime ReportedAt,
    decimal GoodQuantity,
    decimal ScrapQuantity,
    string? Notes) : ITenantRequest<ProductionConfirmationResponse>;
