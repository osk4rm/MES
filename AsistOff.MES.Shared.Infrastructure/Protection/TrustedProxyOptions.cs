using System.Net;

namespace AsistOff.MES.Shared.Infrastructure.Protection;

/// <summary>
/// Explicit trusted-proxy allowlist for the ASP.NET Core Forwarded Headers
/// middleware. Bound from the <c>TrustedProxies</c> configuration section;
/// empty (the default) means default-deny: forwarded headers are never
/// honored and <see cref="AbuseProtectionPolicy"/> partitions by the raw
/// <c>Connection.RemoteIpAddress</c> (the TCP source).
///
/// Behind the shipped nginx reverse proxy (<c>AsistOff.MES.Web/nginx.conf</c>
/// sets <c>X-Forwarded-For: $proxy_add_x_forwarded_for</c>) the operator must
/// list the proxy as trusted so the middleware rewrites
/// <c>RemoteIpAddress</c> to the real client IP before rate limiting runs.
/// Example (docker compose network <c>172.18.0.0/16</c>):
/// <c>TrustedProxies__KnownNetworks__0=172.18.0.0/16</c>, or a single proxy
/// via <c>TrustedProxies__KnownProxies__0=172.18.0.5</c>. Never expose the
/// <c>api</c> container directly to the internet while trusting private
/// ranges — the only ingress must be the trusted compose network behind the
/// proxy. Malformed entries are ignored (never throw at startup).
/// </summary>
public sealed class TrustedProxyOptions
{
    public const string SectionName = "TrustedProxies";

    /// <summary>Single trusted reverse-proxy IPs (e.g. <c>172.18.0.5</c>).</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Trusted proxy networks in CIDR form (e.g. <c>172.18.0.0/16</c>).</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// Parses <see cref="KnownProxies"/>, skipping blank or malformed entries
    /// silently so a typo can never crash boot or open the throttle to
    /// spoofing.
    /// </summary>
    public IPAddress[] GetKnownProxies()
    {
        if (KnownProxies is null || KnownProxies.Length == 0)
        {
            return [];
        }

        var result = new List<IPAddress>(KnownProxies.Length);
        foreach (var entry in KnownProxies)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            if (IPAddress.TryParse(entry.Trim(), out var proxy))
            {
                result.Add(proxy);
            }
        }

        return [.. result];
    }

    /// <summary>
    /// Parses <see cref="KnownNetworks"/> CIDRs, skipping blank or malformed
    /// entries silently (same fail-closed rationale as
    /// <see cref="GetKnownProxies"/>).
    /// </summary>
    public IPNetwork[] GetKnownNetworks()
    {
        if (KnownNetworks is null || KnownNetworks.Length == 0)
        {
            return [];
        }

        var result = new List<IPNetwork>(KnownNetworks.Length);
        foreach (var entry in KnownNetworks)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            if (IPNetwork.TryParse(entry.Trim(), out var network))
            {
                result.Add(network);
            }
        }

        return [.. result];
    }
}
