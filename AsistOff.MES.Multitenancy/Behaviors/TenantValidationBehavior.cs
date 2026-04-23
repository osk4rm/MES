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
        if (request is not ITenantRequest)
            return await next(cancellationToken);

        try
        {
            var tenantId = tenantContext.TenantId;
            if (tenantId == Guid.Empty)
            {
                logger.LogWarning("Tenant request received with empty tenant ID");
                throw new UnauthorizedAccessException("No valid tenant found for this request");
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve tenant identity for request {RequestType}", typeof(TRequest).Name);
            throw new UnauthorizedAccessException("No valid tenant found for this request");
        }

        return await next(cancellationToken);
    }
}