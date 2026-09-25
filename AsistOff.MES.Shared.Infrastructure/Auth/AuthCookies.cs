using AsistOff.MES.Shared.Abstractions.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

/// <summary>
/// HttpOnly cookie transport for auth tokens (issue #241, slice 1 of 2).
/// Moves the session out of browser <c>localStorage</c> so JavaScript can never
/// read tokens: sign-in and refresh set <c>HttpOnly + Secure + SameSite=Lax +
/// Path=/</c> cookies, sign-out clears them with an expired <c>Set-Cookie</c>.
/// Cookie lifetimes honor the configured token lifetimes (access honors the
/// legacy <c>auth:Expiry</c> precedence via <see cref="AuthManager"/>).
/// CSRF defense is <c>SameSite=Lax</c> plus an <c>Origin</c> check on cookie
/// writes (see <see cref="ValidateOrigin"/>).
/// </summary>
public static class AuthCookies
{
    public const string AccessCookieName = "mes_access";
    public const string RefreshCookieName = "mes_refresh";

    public static CookieOptions BuildAccessOptions(AuthOptions options, DateTimeOffset now)
    {
        var lifetime = AuthManager.GetAccessLifetime(options);

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = now.Add(lifetime),
            MaxAge = lifetime,
            IsEssential = true
        };
    }

    public static CookieOptions BuildRefreshOptions(AuthOptions options, DateTimeOffset now)
    {
        var lifetime = options.RefreshTokenLifetime;

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = now.Add(lifetime),
            MaxAge = lifetime,
            IsEssential = true
        };
    }

    public static CookieOptions BuildClearOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = DateTimeOffset.UnixEpoch,
        MaxAge = TimeSpan.Zero,
        IsEssential = true
    };

    public static void AppendAuthCookies(
        HttpResponse response,
        string accessToken,
        string refreshToken,
        AuthOptions options,
        DateTimeOffset now)
    {
        response.Cookies.Append(AccessCookieName, accessToken, BuildAccessOptions(options, now));
        response.Cookies.Append(RefreshCookieName, refreshToken, BuildRefreshOptions(options, now));
    }

    public static void ClearAuthCookies(HttpResponse response)
    {
        var clear = BuildClearOptions();
        response.Cookies.Append(AccessCookieName, string.Empty, clear);
        response.Cookies.Append(RefreshCookieName, string.Empty, clear);
    }

    public static bool TryGetAccessToken(HttpRequest request, out string? accessToken)
    {
        if (request.Cookies.TryGetValue(AccessCookieName, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            accessToken = value;
            return true;
        }

        accessToken = null;
        return false;
    }

    public static bool TryGetRefreshToken(HttpRequest request, out string? refreshToken)
    {
        if (request.Cookies.TryGetValue(RefreshCookieName, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            refreshToken = value;
            return true;
        }

        refreshToken = null;
        return false;
    }

    /// <summary>
    /// CSRF guard for cookie writes. Requests without an <c>Origin</c> header
    /// (same-origin form posts, non-browser clients, tests) pass. When
    /// <c>Origin</c> is present its host must match the request host or one of
    /// the configured <c>cors:allowedOrigins</c>; otherwise the write is
    /// rejected with <c>403</c> so a malicious site cannot mint or rotate a
    /// session through the victim's browser.
    /// </summary>
    public static void ValidateOrigin(HttpRequest request, IConfiguration? configuration = null)
    {
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            throw new ForbiddenException("Cross-origin request rejected");
        }

        if (string.Equals(originUri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var allowed = configuration?.GetSection("cors:allowedOrigins").Get<string[]>() ?? [];
        foreach (var candidate in allowed)
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var allowedUri)
                && string.Equals(allowedUri.Host, originUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new ForbiddenException("Cross-origin request rejected");
    }
}
