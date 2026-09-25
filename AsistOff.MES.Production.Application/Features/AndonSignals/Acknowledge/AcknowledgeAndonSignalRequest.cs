using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Acknowledge;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record AcknowledgeAndonSignalRequest(Guid Id) : ITenantRequest<AndonSignalResponse>;
