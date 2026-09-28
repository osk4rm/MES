using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.ProductionOrders.Resume;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record ResumeProductionOrderRequest(Guid Id, string? ConcurrencyToken = null) : ITenantRequest<ProductionOrderResponse>;
