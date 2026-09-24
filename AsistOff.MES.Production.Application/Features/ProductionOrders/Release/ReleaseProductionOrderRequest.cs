using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Release;

public record ReleaseProductionOrderRequest(Guid Id) : ITenantRequest<ProductionOrderResponse>;
