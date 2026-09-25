using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Rbac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Infrastructure.Seeding;

/// <summary>
/// Idempotent per-tenant provisioner for the parity RBAC rows.
/// Uses <c>IgnoreQueryFilters</c> with an explicit <c>TenantId</c> predicate because
/// it runs in system flows (startup seeder, tenant-created event) with no ambient
/// tenant. The scope is always a single explicit tenant id, never a cross-tenant sweep.
/// </summary>
public sealed class RbacProvisioner(
    DefaultContext context,
    IGuidProvider guidProvider,
    ILogger<RbacProvisioner> logger) : IRbacProvisioner
{
    public async Task ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        // 1. Permissions (union of both parity sets).
        var permissionIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var code in RbacDefaults.AllPermissionCodes)
        {
            var existing = await context.Permissions
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken);

            if (existing is not null)
            {
                permissionIds[code] = existing.Id;
                continue;
            }

            var permission = new Permission
            {
                Id = guidProvider.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = code,
                Category = RbacDefaults.CategoryFor(code),
            };

            await context.Permissions.AddAsync(permission, cancellationToken);
            permissionIds[code] = permission.Id;
        }

        await context.SaveChangesAsync(cancellationToken);

        // 2. Roles + links.
        await EnsureRoleAsync(tenantId, RbacDefaults.AdminRoleCode, RbacDefaults.AdminRoleName,
            RbacDefaults.AdminRoleDescription, RbacDefaults.AdminPermissions, permissionIds, cancellationToken);

        await EnsureRoleAsync(tenantId, RbacDefaults.UserRoleCode, RbacDefaults.UserRoleName,
            RbacDefaults.UserRoleDescription, RbacDefaults.UserPermissions, permissionIds, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Provisioned RBAC parity rows for tenant {TenantId}", tenantId);
    }

    private async Task EnsureRoleAsync(
        Guid tenantId,
        string code,
        string name,
        string description,
        IReadOnlyList<string> permissionCodes,
        Dictionary<string, Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken);

        if (role is null)
        {
            role = new Role
            {
                Id = guidProvider.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                Description = description,
            };

            await context.Roles.AddAsync(role, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        foreach (var permissionCode in permissionCodes)
        {
            var permissionId = permissionIds[permissionCode];

            var linkExists = await context.RolePermissions
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.RoleId == role.Id && x.PermissionId == permissionId,
                    cancellationToken);

            if (linkExists)
            {
                continue;
            }

            await context.RolePermissions.AddAsync(new RolePermission
            {
                Id = guidProvider.NewGuid(),
                TenantId = tenantId,
                RoleId = role.Id,
                PermissionId = permissionId,
            }, cancellationToken);
        }
    }
}
