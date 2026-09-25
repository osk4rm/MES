using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Application.Features.Roles.Responses;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Users.Application.Features.Roles.Browse;

/// <summary>
/// Lists all roles of the caller's tenant with their granted permission codes
/// and member counts. Tenant-admin only.
/// </summary>
[RequirePermission(RbacDefaults.TenantAdmin)]
public sealed record BrowseRolesRequest : ITenantRequest<IReadOnlyCollection<RoleResponse>>;
