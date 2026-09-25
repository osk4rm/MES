using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Update;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record UpdateReasonCodeRequest(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    ReasonCodeCategory Category,
    bool IsActive,
    int SortIndex) : ITenantRequest;
