using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Get;

public record GetProductionOrderRequest(Guid Id) : ITenantRequest<ProductionOrderResponse>;
