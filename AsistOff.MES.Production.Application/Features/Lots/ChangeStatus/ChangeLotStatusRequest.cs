using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.Lots.ChangeStatus;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record ChangeLotStatusRequest(Guid Id, LotStatus Status) : ITenantRequest;
