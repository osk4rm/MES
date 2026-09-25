namespace AsistOff.MES.Shared.Infrastructure.Protection;

/// <summary>
/// Per-IP fixed-window throttle budgets for the anonymous bootstrap endpoints
/// (sign-in and tenant self-registration). Bound from the
/// <c>RateLimiting</c> configuration section; see <c>appsettings.json</c>.
///
/// Defaults are deliberately generous (100 sign-ins and 60 tenant creates per
/// minute per IP): shared shopfloor terminals behind one NAT address must not
/// be locked out by normal shift-change traffic. The throttle only bites on
/// automated credential-stuffing / signup-enumeration bursts. Deployments
/// needing stricter budgets lower the values without a code change.
/// </summary>
public sealed class AbuseProtectionOptions
{
    public const string SectionName = "RateLimiting";

    public ThrottleBudget SignIn { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    public ThrottleBudget TenantCreate { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    public sealed class ThrottleBudget
    {
        /// <summary>Maximum requests per <see cref="WindowSeconds"/> per client IP.</summary>
        public int PermitLimit { get; set; } = 10;

        /// <summary>Fixed window length in seconds.</summary>
        public int WindowSeconds { get; set; } = 60;

        public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
    }
}
