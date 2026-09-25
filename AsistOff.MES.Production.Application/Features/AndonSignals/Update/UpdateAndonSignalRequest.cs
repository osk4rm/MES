using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Update;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateAndonSignalRequest(
    Guid Id,
    AndonSignalCategory Category,
    Guid? ReasonCodeId,
    string? Notes) : ITenantRequest<AndonSignalResponse>;
