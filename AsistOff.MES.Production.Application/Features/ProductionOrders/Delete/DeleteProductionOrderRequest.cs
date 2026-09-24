using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Delete;

public record DeleteProductionOrderRequest(Guid Id) : ITenantRequest;
