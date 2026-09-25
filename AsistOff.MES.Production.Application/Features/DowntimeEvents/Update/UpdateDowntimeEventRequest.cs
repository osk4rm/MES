using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Update;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record UpdateDowntimeEventRequest(
    Guid Id,
    Guid ReasonCodeId,
    string? Notes) : ITenantRequest;
