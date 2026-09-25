using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.Shifts.Update;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record UpdateShiftRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive) : ITenantRequest;
