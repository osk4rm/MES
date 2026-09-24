using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations.Delete;

public record DeleteProductionConfirmationRequest(Guid Id) : ITenantRequest;
