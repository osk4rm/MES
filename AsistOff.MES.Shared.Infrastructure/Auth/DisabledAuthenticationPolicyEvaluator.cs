using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

/// <summary>
/// Development-only authentication bypass (issue #332).
/// Registered by <c>Extensions.AddAuth</c> solely when
/// <c>auth:AuthenticationDisabled=true</c> in the Development environment;
/// any other environment fails fast at startup instead of registering this
/// evaluator. While registered, every authorization check succeeds with no
/// credential and no tenant attribution, so the bypass must never be enabled
/// silently — see <see cref="AuthenticationDisabledWarningService"/> for the
/// startup warning. Each bypassed check emits a debug log per request.
/// </summary>
internal sealed class DisabledAuthenticationPolicyEvaluator(ILogger<DisabledAuthenticationPolicyEvaluator> logger)
    : IPolicyEvaluator
{
    public Task<AuthenticateResult> AuthenticateAsync(AuthorizationPolicy policy, HttpContext context)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Authentication bypassed by auth:AuthenticationDisabled (Development only): succeeding authentication for {Method} {Path} with no credential.",
                context.Request.Method,
                context.Request.Path);
        }

        var authenticationTicket = new AuthenticationTicket(new ClaimsPrincipal(),
            new AuthenticationProperties(), JwtBearerDefaults.AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(authenticationTicket));
    }

    public Task<PolicyAuthorizationResult> AuthorizeAsync(AuthorizationPolicy policy,
        AuthenticateResult authenticationResult, HttpContext context, object? resource)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Authorization bypassed by auth:AuthenticationDisabled (Development only): succeeding authorization for {Method} {Path} with no permission checks.",
                context.Request.Method,
                context.Request.Path);
        }

        return Task.FromResult(PolicyAuthorizationResult.Success());
    }
}