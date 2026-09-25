using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Application.Features.Roles.Responses;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Users.Application.Features.Roles.Get;

/// <summary>
/// Returns a single tenant role with its granted permissions and members.
/// Unknown or cross-tenant ids surface as 404 (the tenant query filter hides
/// other tenants' rows, so no data leaks). Tenant-admin only.
/// </summary>
[RequirePermission(RbacDefaults.TenantAdmin)]
public sealed record GetRoleRequest(Guid Id) : ITenantRequest<RoleDetailResponse>;
