using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Delete;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record DeleteProductionOrderRequest(Guid Id) : ITenantRequest;
