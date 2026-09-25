namespace AsistOff.MES.Users.Core.Rbac;

/// <summary>
/// Provisions the parity RBAC rows (roles, permissions, links) for one tenant.
/// Implemented in Users.Infrastructure (needs DefaultContext); consumed by the
/// tenant-created listener and the startup backfill seeder.
/// </summary>
public interface IRbacProvisioner
{
    Task ProvisionAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
