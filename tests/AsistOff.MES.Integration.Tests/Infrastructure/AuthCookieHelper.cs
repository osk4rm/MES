namespace AsistOff.MES.Integration.Tests.Infrastructure;

/// <summary>
/// Helpers for the httpOnly auth-cookie transport (issue #241): sign-in and
/// refresh issue the session via <c>Set-Cookie</c> (<c>mes_access</c> /
/// <c>mes_refresh</c>) with no usable tokens in the body, and the test
/// <c>HttpClient</c> does not persist cookies automatically — so tests parse
/// <c>Set-Cookie</c> and forward a <c>Cookie</c> header explicitly.
/// </summary>
public static class AuthCookieHelper
{
    public const string AccessCookieName = "mes_access";
    public const string RefreshCookieName = "mes_refresh";

    /// <summary>All <c>Set-Cookie</c> values of a response (empty when none).</summary>
    public static IReadOnlyList<string> GetSetCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return [];
        }

        return values.ToList();
    }

    /// <summary>Value of the named cookie from the response's <c>Set-Cookie</c> headers.</summary>
    public static string? GetCookieValue(HttpResponseMessage response, string name)
    {
        foreach (var setCookie in GetSetCookies(response))
        {
            var pair = setCookie.Split(';', 2)[0];
            var equals = pair.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            if (string.Equals(pair[..equals].Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return pair[(equals + 1)..].Trim();
            }
        }

        return null;
    }

    public static string? GetAccessToken(HttpResponseMessage response)
        => GetCookieValue(response, AccessCookieName);

    public static string? GetRefreshToken(HttpResponseMessage response)
        => GetCookieValue(response, RefreshCookieName);

    /// <summary>
    /// Builds a <c>Cookie</c> header value forwarding the auth cookies of a
    /// sign-in/refresh response (or any explicit name/value pairs).
    /// </summary>
    public static string BuildCookieHeader(HttpResponseMessage response)
    {
        var pairs = new List<string>();
        var access = GetAccessToken(response);
        if (!string.IsNullOrEmpty(access))
        {
            pairs.Add($"{AccessCookieName}={access}");
        }

        var refresh = GetRefreshToken(response);
        if (!string.IsNullOrEmpty(refresh))
        {
            pairs.Add($"{RefreshCookieName}={refresh}");
        }

        return string.Join("; ", pairs);
    }

    public static string BuildCookieHeader(string? accessToken, string? refreshToken)
    {
        var pairs = new List<string>();
        if (!string.IsNullOrEmpty(accessToken))
        {
            pairs.Add($"{AccessCookieName}={accessToken}");
        }

        if (!string.IsNullOrEmpty(refreshToken))
        {
            pairs.Add($"{RefreshCookieName}={refreshToken}");
        }

        return string.Join("; ", pairs);
    }

    /// <summary>Returns a client presenting only the given cookies (no Authorization header).</summary>
    public static HttpClient CreateCookieClient(MesApplicationFixture fixture, string cookieHeader)
    {
        var client = fixture.CreateClient();
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        }

        return client;
    }
}
