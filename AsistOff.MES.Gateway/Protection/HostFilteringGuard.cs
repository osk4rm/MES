namespace AsistOff.MES.Gateway.Protection;

/// <summary>
/// Fail-fast guard for the ASP.NET Core host-header allowlist (issue #369).
/// The shipped <c>appsettings.json</c> defaults <c>AllowedHosts</c> to the
/// loopback hosts (<c>localhost;127.0.0.1;[::1]</c>, fail-closed); the
/// Development override alone opts into the <c>*</c> wildcard for local
/// ergonomics (containers, tunnels, LAN IPs).
/// A wildcard outside Development never boots: a forged <c>Host</c> header
/// could otherwise poison cached links or password-reset URLs across tenants.
/// Set the public host(s) via the <c>AllowedHosts</c> configuration key
/// (e.g. the <c>AllowedHosts</c> environment variable or
/// <c>AllowedHosts="mes.example.com;www.mes.example.com"</c> in Coolify).
/// </summary>
public static class HostFilteringGuard
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when
    /// <paramref name="allowedHosts"/> contains a wildcard entry and
    /// <paramref name="isDevelopment"/> is false. Null, empty, or explicit
    /// host lists always pass (an explicit list — including the loopback
    /// default — is the safe configuration).
    /// </summary>
    public static void Validate(string? allowedHosts, bool isDevelopment)
    {
        if (!isDevelopment && IsWildcard(allowedHosts))
        {
            throw new InvalidOperationException(
                "AllowedHosts must not contain a wildcard ('*' or '*.'-prefixed entry) outside Development. " +
                "Set AllowedHosts to the public host name(s), e.g. AllowedHosts=\"mes.example.com\".");
        }
    }

    /// <summary>
    /// True when any <c>;</c>- or <c>,</c>-separated entry is a wildcard:
    /// exactly <c>*</c> or a sub-domain wildcard such as
    /// <c>*.example.com</c>. Comparison is ordinal after trimming; matching
    /// mirrors the HostFiltering middleware semantics where <c>*</c> disables
    /// validation entirely.
    /// </summary>
    public static bool IsWildcard(string? allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(allowedHosts))
        {
            return false;
        }

        return allowedHosts
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .Any(entry => entry == "*" || entry.StartsWith("*.", StringComparison.Ordinal));
    }
}
