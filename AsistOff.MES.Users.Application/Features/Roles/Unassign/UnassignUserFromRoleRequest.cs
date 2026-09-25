using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Users.Application.Features.Roles.Unassign;

/// <summary>
/// Removes a user's assignment to a role. The user loses the role's
/// permissions in their next sign-in claim. Unknown or cross-tenant role ids,
/// and assignments that do not exist, surface as 404. Tenant-admin only.
/// </summary>
[RequirePermission(RbacDefaults.TenantAdmin)]
public sealed record UnassignUserFromRoleRequest(
    Guid RoleId,
    Guid UserId) : ITenantRequest;
