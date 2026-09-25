using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents.Close;

[RequirePermission(RbacDefaults.ProductionWrite)]
public record CloseDowntimeEventRequest(
    Guid Id,
    DateTime? EndedAt) : ITenantRequest<DowntimeEventResponse>;
