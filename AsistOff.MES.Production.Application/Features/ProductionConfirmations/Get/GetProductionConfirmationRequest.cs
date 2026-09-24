using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Get;

public record GetProductionConfirmationRequest(Guid Id) : ITenantRequest<ProductionConfirmationResponse>;
