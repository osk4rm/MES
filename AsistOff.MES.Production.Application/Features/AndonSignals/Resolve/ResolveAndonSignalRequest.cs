using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Resolve;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record ResolveAndonSignalRequest(Guid Id, DateTime? ResolvedAt) : ITenantRequest<AndonSignalResponse>;
