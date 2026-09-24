using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Complete;

public record CompleteProductionOrderRequest(Guid Id) : ITenantRequest<ProductionOrderResponse>;
