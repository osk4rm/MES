using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Users.Application.Features.Roles.Update;

/// <summary>
/// Updates role metadata (name and description). The code is immutable after
/// creation. Unknown or cross-tenant ids surface as 404. Tenant-admin only.
/// </summary>
[RequirePermission(RbacDefaults.TenantAdmin)]
public sealed record UpdateRoleRequest(
    Guid Id,
    string Name,
    string? Description) : ITenantRequest;
