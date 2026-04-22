using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Requests;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Multitenancy.Behaviors;

public class TenantValidationBehavior<TRequest, TResponse>(
    ITenantContext tenantContext,
    ILogger<TenantValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITenantRequest || tenantContext.TenantId != Guid.Empty) 
            return await next(cancellationToken);

        logger.LogWarning("Tenant request without valid tenant ID {TenantId}", tenantContext.TenantId);
        throw new UnauthorizedAccessException("No valid tenant found for this request");
    }
}