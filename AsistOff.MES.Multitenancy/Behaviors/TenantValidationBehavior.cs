using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Multitenancy.Behaviors;

/// <summary>
/// Pipeline behavior that enforces the presence of an ambient tenant context
/// for every MediatR request, unless the request is explicitly marked with
/// <see cref="IAllowAnonymousRequest"/> (e.g. sign‑in, tenant provisioning).
///
/// This is defense in depth on top of the EF Core global query filter: the
/// filter silently restricts reads/writes to the current tenant, while this
/// behavior fails fast with <see cref="UnauthorizedAccessException"/> so
/// operators see a clear 401 instead of an empty result set.
/// </summary>
public class TenantValidationBehavior<TRequest, TResponse>(
    ICurrentTenantAccessor tenantAccessor,
    ILogger<TenantValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IAllowAnonymousRequest)
        {
            return await next(cancellationToken);
        }

        if (!tenantAccessor.TryGetTenantId(out _))
        {
            logger.LogWarning("Request {RequestType} rejected: no valid tenant context", typeof(TRequest).Name);
            throw new UnauthorizedAccessException("No valid tenant found for this request");
        }

        return await next(cancellationToken);
    }
}