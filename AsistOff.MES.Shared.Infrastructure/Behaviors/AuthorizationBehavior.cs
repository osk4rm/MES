using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Shared.Infrastructure.Behaviors
{
    /// <summary>
    /// MediatR pipeline step enforcing declarative <see cref="RequirePermissionAttribute"/>
    /// permissions. Requests without the attribute pass through unchanged; requests
    /// carrying it require the caller to hold every declared permission, otherwise
    /// a <see cref="ForbiddenException"/> (HTTP 403) is thrown.
    /// </summary>
    public class AuthorizationBehavior<TRequest, TResponse>(
        ICurrentPermissionsAccessor permissionsAccessor,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var required = typeof(TRequest)
                .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
                .Cast<RequirePermissionAttribute>()
                .Select(a => a.Permission)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (required.Length == 0)
                return await next(cancellationToken);

            var granted = permissionsAccessor.Permissions;
            var missing = required.Where(p => !granted.Contains(p, StringComparer.Ordinal)).ToArray();

            if (missing.Length > 0)
            {
                logger.LogWarning("Request {RequestType} rejected: missing permission(s) {Permissions}",
                    typeof(TRequest).Name, string.Join(", ", missing));
                throw new ForbiddenException($"Missing required permission(s): {string.Join(", ", missing)}.");
            }

            return await next(cancellationToken);
        }
    }
}
