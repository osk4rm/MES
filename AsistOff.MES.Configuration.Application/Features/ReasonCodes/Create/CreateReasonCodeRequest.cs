using AsistOff.MES.Configuration.Application.Features.ReasonCodes.Responses;
using AsistOff.MES.Configuration.Domain.Enums;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Create;

[RequirePermission(RbacDefaults.ConfigurationWrite)]
public record CreateReasonCodeRequest(
    string Code,
    string Name,
    string? Description,
    ReasonCodeCategory Category,
    bool IsActive,
    int SortIndex) : ITenantRequest<ReasonCodeResponse>;
