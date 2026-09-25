using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;

namespace AsistOff.MES.Users.Application.Features.Roles.Assign;

/// <summary>
/// Assigns a tenant user to a role. The user sees the role's permissions in
/// their next sign-in claim. Idempotent: assigning an already-assigned user is
/// a no-op. Unknown or cross-tenant role and user ids surface as 404.
/// Tenant-admin only.
/// </summary>
[RequirePermission("tenant.admin")]
public sealed record AssignUserToRoleRequest(
    Guid RoleId,
    Guid UserId) : ITenantRequest;
