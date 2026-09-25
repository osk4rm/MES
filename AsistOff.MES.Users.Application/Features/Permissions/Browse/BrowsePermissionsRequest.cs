using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Application.Features.Roles.Responses;
using AsistOff.MES.Users.Core.Rbac;

namespace AsistOff.MES.Users.Application.Features.Permissions.Browse;

/// <summary>
/// Lists all permissions of the caller's tenant. Feeds the back-office
/// permission matrix columns. Tenant-admin only.
/// </summary>
[RequirePermission(RbacDefaults.TenantAdmin)]
public sealed record BrowsePermissionsRequest : ITenantRequest<IReadOnlyCollection<PermissionResponse>>;
