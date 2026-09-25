using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Users.Application.Features.Roles.SetPermissions;

/// <summary>
/// Replaces the permission collection granted by a role. Members of the role
/// see the new set in their next sign-in permissions claim. Unknown or
/// cross-tenant role and permission ids surface as 404. Tenant-admin only.
/// </summary>
[RequirePermission(RbacDefaults.TenantAdmin)]
public sealed record SetRolePermissionsRequest(
    Guid RoleId,
    IReadOnlyCollection<Guid> PermissionIds) : ITenantRequest;
