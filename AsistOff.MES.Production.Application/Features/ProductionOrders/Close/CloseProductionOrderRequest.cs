using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Close;

public record CloseProductionOrderRequest(Guid Id) : ITenantRequest<ProductionOrderResponse>;
