using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

/// <summary>
/// httpOnly cookie transport for auth tokens (tracker slice #241, ADR-0003).
/// Manufacturing customers run shared shopfloor terminals where JWTs in
/// browser <c>localStorage</c> are exposed to XSS theft, so access and refresh
/// tokens travel in <c>HttpOnly</c> cookies that JavaScript can never read.
/// The <c>Authorization</c> header keeps working during transition (see the
/// <c>OnMessageReceived</c> fallback in <see cref="Extensions"/>); cookie-only
/// callers are first-class.
/// </summary>
public static class AuthCookies
{
    /// <summary>Short-lived JWT access token cookie.</summary>
    public const string AccessCookieName = "mes_access";

    /// <summary>Opaque server-side refresh token cookie.</summary>
    public const string RefreshCookieName = "mes_refresh";

    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Lifetime of the access cookie, mirroring the access-token lifetime
    /// (<see cref="AuthOptions.Expiry"/> takes precedence for backwards
    /// compatibility, otherwise <see cref="AuthOptions.AccessTokenLifetime"/> —
    /// same precedence as <c>AuthManager</c> token creation).
    /// </summary>
    public static TimeSpan GetAccessCookieLifetime(AuthOptions options)
        => options.Expiry != TimeSpan.Zero ? options.Expiry : options.AccessTokenLifetime;

    /// <summary>
    /// Hardened options for the access cookie: <c>HttpOnly</c>, <c>Secure</c>,
    /// <c>SameSite=Lax</c>, <c>Path=/</c>, expiring with the access token.
    /// </summary>
    public static CookieOptions BuildAccessOptions(AuthOptions options) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true,
        MaxAge = GetAccessCookieLifetime(options)
    };

    /// <summary>
    /// Hardened options for the refresh cookie: <c>HttpOnly</c>, <c>Secure</c>,
    /// <c>SameSite=Lax</c>, <c>Path=/</c>, expiring with the refresh token.
    /// </summary>
    public static CookieOptions BuildRefreshOptions(AuthOptions options) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true,
        MaxAge = options.RefreshTokenLifetime
    };

    /// <summary>
    /// Sets fresh access and refresh cookies after sign-in or refresh rotation.
    /// Takes <see cref="IResponseCookies"/> (rather than <c>HttpResponse</c>)
    /// so the cookie contract stays unit-testable without a server.
    /// </summary>
    public static void AppendAuthCookies(IResponseCookies cookies, JsonWebToken tokens, AuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(options);

        cookies.Append(AccessCookieName, tokens.AccessToken, BuildAccessOptions(options));
        cookies.Append(RefreshCookieName, tokens.RefreshToken, BuildRefreshOptions(options));
    }

    /// <summary>
    /// Expires both auth cookies on sign-out. The options repeat the original
    /// <c>Path</c>/<c>SameSite</c>/<c>Secure</c> so browsers match and delete
    /// the exact cookies that were set.
    /// </summary>
    public static void ClearAuthCookies(IResponseCookies cookies)
    {
        ArgumentNullException.ThrowIfNull(cookies);

        var expired = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            Expires = DateTimeOffset.UnixEpoch,
            MaxAge = TimeSpan.Zero
        };

        cookies.Append(AccessCookieName, string.Empty, expired);
        cookies.Append(RefreshCookieName, string.Empty, expired);
    }

    /// <summary>
    /// Resolves the ambient access token for binding checks: the
    /// <c>Authorization: Bearer</c> header wins when present, otherwise the
    /// access cookie. Returns <c>null</c> when neither carries a token.
    /// </summary>
    public static string? GetAccessToken(string? authorizationHeader, string? accessCookie)
    {
        if (!string.IsNullOrWhiteSpace(authorizationHeader) &&
            authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = authorizationHeader[BearerPrefix.Length..].Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return string.IsNullOrWhiteSpace(accessCookie) ? null : accessCookie;
    }

    /// <summary>
    /// CSRF backstop for the cookie-writing endpoints (<c>sign-in</c>,
    /// <c>refresh</c>, <c>sign-out</c>), complementing <c>SameSite=Lax</c>.
    /// Non-browser callers send no <c>Origin</c> header and pass untouched;
    /// when the header is present its host must equal the request host,
    /// otherwise the request is rejected with 400. Ports and schemes are
    /// deliberately ignored so same-host dev proxies keep working.
    /// </summary>
    /// <exception cref="ValidationException">Origin host differs from the request host, or is malformed.</exception>
    public static void RequireSameOrigin(string? originHeader, string? requestHost)
    {
        if (string.IsNullOrWhiteSpace(originHeader))
        {
            return;
        }

        if (!Uri.TryCreate(originHeader, UriKind.Absolute, out var originUri))
        {
            throw new ValidationException("Invalid Origin header");
        }

        if (!string.Equals(originUri.Host, requestHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Cross-origin request rejected");
        }
    }
}
