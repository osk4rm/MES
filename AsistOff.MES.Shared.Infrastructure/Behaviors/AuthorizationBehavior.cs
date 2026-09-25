using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Shared.Infrastructure.Behaviors
{
    /// <summary>
    /// MediatR pipeline step enforcing declarative <see cref="RequirePermissionAttribute"/>
    /// permissions with default-deny for all modules (issues #231 slice 1/2 and
    /// #233 slice 2/2: Users, Multitenancy, Configuration, Production, Attachments).
    ///
    /// Resolution order:
    /// <list type="number">
    /// <item>Requests carrying <see cref="RequirePermissionAttribute"/> require the caller
    /// to hold every declared permission, otherwise a <see cref="ForbiddenException"/>
    /// (HTTP 403) is thrown.</item>
    /// <item>Requests on the documented <see cref="AuthorizationAllowlist"/> (reads,
    /// session maintenance, anonymous bootstrap) pass through unchanged.</item>
    /// <item>Requests from the legacy pass-through assemblies
    /// (<see cref="AuthorizationAllowlist.LegacyPassthroughAssemblyNames"/>, empty
    /// since slice 2/2) pass through unchanged.</item>
    /// <item>Anything else is rejected with <see cref="ForbiddenException"/> instead of
    /// executing, so a new write added without coverage fails closed.</item>
    /// </list>
    ///
    /// The generic constraint is deliberately <see cref="IBaseRequest"/> rather than
    /// <c>IRequest&lt;TResponse&gt;</c>: since MediatR.Contracts 2.x the non-generic
    /// <c>IRequest</c> (void commands, resolved in the pipeline as <c>TResponse = Unit</c>)
    /// no longer extends <c>IRequest&lt;Unit&gt;</c> — both are siblings under
    /// <c>IBaseRequest</c>. Constraining on <c>IRequest&lt;TResponse&gt;</c> silently
    /// drops void requests from this behavior, leaving their
    /// <c>RequirePermission</c> attributes unenforced (403 becomes 404/204).
    /// </summary>
    public class AuthorizationBehavior<TRequest, TResponse>(
        ICurrentPermissionsAccessor permissionsAccessor,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IBaseRequest
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestType = typeof(TRequest);
            var required = requestType
                .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
                .Cast<RequirePermissionAttribute>()
                .Select(a => a.Permission)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (required.Length > 0)
            {
                var granted = permissionsAccessor.Permissions;
                var missing = required.Where(p => !granted.Contains(p, StringComparer.Ordinal)).ToArray();

                if (missing.Length > 0)
                {
                    logger.LogWarning("Request {RequestType} rejected: missing permission(s) {Permissions}",
                        requestType.Name, string.Join(", ", missing));
                    throw new ForbiddenException($"Missing required permission(s): {string.Join(", ", missing)}.");
                }

                return await next(cancellationToken);
            }

            if (AuthorizationAllowlist.IsAllowed(requestType))
                return await next(cancellationToken);

            if (AuthorizationAllowlist.IsLegacyPassthrough(requestType))
            {
                logger.LogDebug("Request {RequestType} passes through without permission (legacy slice-2 module).",
                    requestType.FullName);
                return await next(cancellationToken);
            }

            logger.LogWarning("Request {RequestType} rejected: no RequirePermission and no allowlist entry (default-deny).",
                requestType.FullName);
            throw new ForbiddenException(
                $"Request {requestType.Name} requires a permission declaration or an allowlist entry.");
        }
    }
}
