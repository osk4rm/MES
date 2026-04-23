namespace AsistOff.MES.Multitenancy.Contracts.Interfaces;

/// <summary>
/// Marker interface for MediatR requests that deliberately run outside of a
/// tenant scope (e.g. sign‑in, tenant provisioning, health checks).
///
/// By default every request is treated as tenant‑scoped and must satisfy
/// <see cref="ITenantRequest"/> — requests implementing this marker opt out
/// of the tenant validation pipeline behavior.
/// </summary>
public interface IAllowAnonymousRequest
{
}
