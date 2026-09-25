using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

/// <summary>
/// HttpOnly cookie transport for auth tokens (issue #241, slice 1 of 2).
/// Shopfloor terminals share browsers where JWTs in <c>localStorage</c> are
/// exposed to XSS theft (ADR-0003 known limitation), so the session moves
/// into httpOnly cookies that JavaScript can never read. The
/// <c>Authorization</c> header keeps working during transition (see
/// <c>Extensions.AddAuth</c>); the Web frontend migrates in slice 2.
/// </summary>
public static class AuthCookies
{
    /// <summary>Short-lived JWT access token cookie.</summary>
    public const string AccessCookieName = "mes_access";

    /// <summary>Opaque server-side refresh token cookie.</summary>
    public const string RefreshCookieName = "mes_refresh";

    /// <summary>
    /// Effective lifetime of the access cookie. Honors the legacy
    /// <c>auth:Expiry</c> override for backwards compatibility, otherwise the
    /// configured <c>auth:AccessTokenLifetime</c>.
    /// </summary>
    public static TimeSpan GetAccessLifetime(AuthOptions options)
        => options.Expiry != TimeSpan.Zero ? options.Expiry : options.AccessTokenLifetime;

    /// <summary>
    /// Builds the hardened options shared by both auth cookies: HttpOnly so
    /// JavaScript can never read the tokens, Secure so they only travel over
    /// TLS, SameSite=Lax as the CSRF baseline (plus an Origin check on cookie
    /// writes in <c>AuthenticationController</c>), and Path=/ so every API
    /// call carries the session.
    /// </summary>
    public static CookieOptions BuildCookieOptions(TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = lifetime,
        Expires = DateTimeOffset.UtcNow.Add(lifetime)
    };

    /// <summary>Options for the access cookie, expiring with the JWT.</summary>
    public static CookieOptions BuildAccessCookieOptions(AuthOptions options)
        => BuildCookieOptions(GetAccessLifetime(options));

    /// <summary>Options for the refresh cookie, expiring with the opaque token.</summary>
    public static CookieOptions BuildRefreshCookieOptions(AuthOptions options)
        => BuildCookieOptions(options.RefreshTokenLifetime);

    /// <summary>
    /// Issues fresh access + refresh cookies. Cookie lifetimes honor the
    /// configured token lifetimes so sessions and cookies expire together.
    /// </summary>
    public static void AppendAuthCookies(
        HttpResponse response, string accessToken, string refreshToken, AuthOptions options)
    {
        response.Cookies.Append(AccessCookieName, accessToken, BuildAccessCookieOptions(options));
        response.Cookies.Append(RefreshCookieName, refreshToken, BuildRefreshCookieOptions(options));
    }

    /// <summary>
    /// Clears both auth cookies with immediately-expired <c>Set-Cookie</c>
    /// headers (same name, path and flags so browsers drop them).
    /// </summary>
    public static void ClearAuthCookies(HttpResponse response)
    {
        var expired = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.Zero,
            Expires = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        response.Cookies.Append(AccessCookieName, string.Empty, expired);
        response.Cookies.Append(RefreshCookieName, string.Empty, expired);
    }

    /// <summary>
    /// CSRF guard for cookie-writing auth endpoints. SameSite=Lax already
    /// blocks cross-site POSTs from third-party contexts; this additionally
    /// rejects requests whose <c>Origin</c> host differs from the request
    /// host. Requests without an <c>Origin</c> header (same-origin form posts,
    /// non-browser clients, TestServer) are allowed. Ports are intentionally
    /// ignored so the local Vite dev server (<c>localhost:5173</c>) can call
    /// the API (<c>localhost:5080</c>) on the same host.
    /// </summary>
    public static bool IsOriginAllowed(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            return false;
        }

        return string.Equals(originUri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase);
    }
}
