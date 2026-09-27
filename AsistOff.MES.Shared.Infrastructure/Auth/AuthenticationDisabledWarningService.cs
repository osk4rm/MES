using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

/// <summary>
/// Startup warning for the Development-only authentication bypass (issue #332).
/// Registered by <c>Extensions.AddAuth</c> only when
/// <c>auth:AuthenticationDisabled=true</c> in the Development environment.
/// Logs an unmistakable warning at host startup so the bypass is never enabled
/// silently; every authorization check is bypassed while this service is
/// registered (see <see cref="DisabledAuthenticationPolicyEvaluator"/>).
/// </summary>
public sealed class AuthenticationDisabledWarningService(ILogger<AuthenticationDisabledWarningService> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "WARNING: auth:AuthenticationDisabled=true — all authentication and authorization checks are BYPASSED. " +
            "Every RequirePermission-guarded endpoint allows unauthenticated requests with no tenant attribution. " +
            "Development local-use only; never enable outside Development.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
