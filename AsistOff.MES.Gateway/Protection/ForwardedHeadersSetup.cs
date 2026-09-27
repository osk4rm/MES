using AsistOff.MES.Shared.Infrastructure.Protection;
using Microsoft.AspNetCore.HttpOverrides;

namespace AsistOff.MES.Gateway.Protection;

/// <summary>
/// Applies <see cref="TrustedProxyOptions"/> to <see cref="ForwardedHeadersOptions"/>
/// (issue #323). Default-deny: the framework defaults are cleared first, so an
/// empty allowlist trusts nobody and spoofed <c>X-Forwarded-For</c> can never
/// move the abuse-protection throttle partition. Malformed entries were
/// already skipped by <see cref="TrustedProxyOptions"/> parsing, so this never
/// throws and never opens the throttle to spoofing.
///
/// The Gateway snapshots the allowlist once at startup (see <c>Program.cs</c>)
/// and test hosts pin their own snapshot in
/// <c>MesWebApplicationFactory</c> — both call <see cref="Apply"/> directly
/// instead of depending on a lazy <c>IOptions&lt;TrustedProxyOptions&gt;</c>
/// indirection, which did not deterministically clear the defaults in every
/// host (CI: rotating <c>X-Forwarded-For</c> still escaped the throttle).
/// </summary>
public static class ForwardedHeadersSetup
{
    /// <summary>
    /// Binds the <c>TrustedProxies</c> section once. Never returns null:
    /// a missing section means default-deny (empty allowlist).
    /// </summary>
    public static TrustedProxyOptions Snapshot(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetSection(TrustedProxyOptions.SectionName).Get<TrustedProxyOptions>()
            ?? new TrustedProxyOptions();
    }

    /// <summary>
    /// Clears the framework defaults, then trusts exactly the parsed
    /// proxies/networks. Empty <paramref name="trusted"/> keeps default-deny
    /// by disabling forwarded-headers processing entirely
    /// (<c>ForwardedHeaders.None</c>): the middleware then never rewrites
    /// <c>RemoteIpAddress</c>, so spoofed <c>X-Forwarded-For</c> cannot move
    /// the throttle partition even if a host would otherwise honor an
    /// enabled-but-empty allowlist (issue #323, CI: rotating header got 401
    /// instead of 429). A non-empty allowlist enables
    /// <c>XForwardedFor | XForwardedProto</c> with exactly those entries.
    /// </summary>
    public static void Apply(ForwardedHeadersOptions options, TrustedProxyOptions trusted)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(trusted);

        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        if (!trusted.HasTrustedProxies)
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        foreach (var proxy in trusted.GetKnownProxies())
        {
            options.KnownProxies.Add(proxy);
        }

        foreach (var network in trusted.GetKnownNetworks())
        {
            options.KnownIPNetworks.Add(network);
        }
    }

    /// <summary>
    /// Registers <see cref="Apply"/> for <paramref name="snapshot"/> so callers
    /// that cannot name <see cref="ForwardedHeadersOptions"/> directly (e.g.
    /// test projects without the shared-framework reference) can still pin an
    /// explicit trust. Later registrations win, so test hosts override the
    /// Gateway wiring.
    /// </summary>
    public static void Pin(IServiceCollection services, TrustedProxyOptions snapshot)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(snapshot);

        services.Configure<ForwardedHeadersOptions>(options => Apply(options, snapshot));
    }
}
