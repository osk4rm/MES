using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Application.Features.Roles.Responses;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Users.Application.Features.Roles.Create;

/// <summary>
/// Creates a tenant role. The code is tenant-unique and immutable after
/// creation; permissions and members are assigned via the dedicated endpoints.
/// Tenant-admin only.
/// </summary>
[RequirePermission(RbacDefaults.TenantAdmin)]
public sealed record CreateRoleRequest(
    string Code,
    string Name,
    string? Description) : ITenantRequest<RoleResponse>;
