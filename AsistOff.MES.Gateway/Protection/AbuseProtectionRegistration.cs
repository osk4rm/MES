using System.Threading.RateLimiting;
using AsistOff.MES.Shared.Infrastructure.Protection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AsistOff.MES.Gateway.Protection;

/// <summary>
/// Gateway abuse protection: per-IP fixed-window throttling for the two
/// anonymous bootstrap endpoints (sign-in, tenant self-registration).
/// Everything else bypasses the limiter via a no-op partition, so
/// authenticated traffic is never throttled.
///
/// Budgets are resolved from <see cref="IOptionsMonitor{AbuseProtectionOptions}"/>
/// at request time (never snapshotted at startup) so test hosts can override
/// them via <c>services.Configure</c> after the production binding.
/// </summary>
public static class AbuseProtectionRegistration
{
    public static IServiceCollection AddAbuseProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AbuseProtectionOptions>(
            configuration.GetSection(AbuseProtectionOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = (context, cancellationToken) =>
            {
                if (!context.HttpContext.Response.HasStarted)
                {
                    var budgets = context.HttpContext.RequestServices
                        .GetRequiredService<IOptionsMonitor<AbuseProtectionOptions>>()
                        .CurrentValue;
                    context.HttpContext.Response.Headers.RetryAfter =
                        ResolveRetryAfterSeconds(context, budgets).ToString();
                }

                return ValueTask.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var scope = AbuseProtectionPolicy.MatchScope(context.Request);
                if (scope == AbuseProtectionPolicy.SignInScope)
                {
                    var budgets = context.RequestServices
                        .GetRequiredService<IOptionsMonitor<AbuseProtectionOptions>>()
                        .CurrentValue;
                    return RateLimitPartition.GetFixedWindowLimiter(
                        AbuseProtectionPolicy.BuildPartitionKey(context, scope),
                        _ => FixedWindow(budgets.SignIn));
                }

                if (scope == AbuseProtectionPolicy.TenantCreateScope)
                {
                    var budgets = context.RequestServices
                        .GetRequiredService<IOptionsMonitor<AbuseProtectionOptions>>()
                        .CurrentValue;
                    return RateLimitPartition.GetFixedWindowLimiter(
                        AbuseProtectionPolicy.BuildPartitionKey(context, scope),
                        _ => FixedWindow(budgets.TenantCreate));
                }

                return RateLimitPartition.GetNoLimiter("bypass");
            });
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions FixedWindow(AbuseProtectionOptions.ThrottleBudget budget)
        => new()
        {
            PermitLimit = budget.PermitLimit,
            Window = budget.Window,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true
        };

    private static int ResolveRetryAfterSeconds(OnRejectedContext context, AbuseProtectionOptions budgets)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            return Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        // Lease carried no hint (should not happen for fixed-window): fall
        // back to the configured window of the matched scope.
        var scope = AbuseProtectionPolicy.MatchScope(context.HttpContext.Request);

        return scope switch
        {
            AbuseProtectionPolicy.SignInScope => Math.Max(1, budgets.SignIn.WindowSeconds),
            AbuseProtectionPolicy.TenantCreateScope => Math.Max(1, budgets.TenantCreate.WindowSeconds),
            _ => 60
        };
    }
}
