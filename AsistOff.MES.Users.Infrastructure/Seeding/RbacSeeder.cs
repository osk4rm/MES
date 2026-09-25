using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Shared.Abstractions.Seeder;
using AsistOff.MES.Users.Core.Rbac;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Infrastructure.Seeding;

/// <summary>
/// Startup backfill: ensures every active tenant has the parity RBAC rows.
/// New tenants are covered by <c>TenantCreatedEventListener</c>; this seeder
/// covers tenants that pre-date the RBAC migration. Idempotent.
/// </summary>
public sealed class RbacSeeder(
    ITenantRepository tenants,
    IRbacProvisioner provisioner,
    ILogger<RbacSeeder> logger) : ISeeder
{
    public async Task Seed()
    {
        IReadOnlyCollection<Guid> ids;
        try
        {
            ids = await tenants.ListActiveIdsAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping RBAC seed: tenants table not reachable");
            return;
        }

        foreach (var tenantId in ids)
        {
            try
            {
                await provisioner.ProvisionAsync(tenantId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to provision RBAC rows for tenant {TenantId}", tenantId);
            }
        }
    }
}
