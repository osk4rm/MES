using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Close;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record CloseProductionOrderRequest(Guid Id) : ITenantRequest<ProductionOrderResponse>;
