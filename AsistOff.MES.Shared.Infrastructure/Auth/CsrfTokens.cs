using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Infrastructure.Auth;

/// <summary>
/// Double-submit CSRF tokens for the cookie-writing auth endpoints
/// (issue #376). SameSite=Lax plus the Origin host check in
/// <c>AuthCookies.IsOriginAllowed</c> is the baseline, but a forged
/// cross-site POST carrying ambient cookies would still reach
/// <c>POST /api/auth/refresh</c> or <c>POST /api/auth/sign-out</c> and
/// rotate or kill a live operator session. The per-request token closes
/// the gap: issuance (<c>GET /api/auth/csrf</c>) plants a signed token in
/// the readable <c>mes_csrf</c> cookie and returns the same value in the
/// body; every auth write must echo it back in the <c>X-CSRF-Token</c>
/// header (or the <c>csrfToken</c> JSON field for non-browser clients)
/// and the server only accepts the write when the echoed value equals the
/// cookie value <i>and</i> carries a valid HMAC. The signature binds the
/// token to the server signing key so a subdomain cookie-toss cannot mint
/// a matching pair, and the expiry bounds replay.
/// </summary>
public static class CsrfTokens
{
    /// <summary>Readable (non-httpOnly) cookie carrying the issued token.</summary>
    public const string CookieName = "mes_csrf";

    /// <summary>Header carrying the echoed token on auth writes.</summary>
    public const string HeaderName = "X-CSRF-Token";

    /// <summary>JSON body field carrying the echoed token (non-browser clients).</summary>
    public const string BodyFieldName = "csrfToken";

    /// <summary>Accepted alias of <see cref="BodyFieldName"/> for snake_case callers.</summary>
    public const string BodyFieldAlias = "csrf_token";

    /// <summary>How long an issued token validates. Independent of session lifetimes.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Issues a signed token: <c>base64url(nonce).expiryUnix.base64url(hmac)</c>
    /// where the HMAC (keyed by the JWT signing key) covers nonce + expiry.
    /// </summary>
    public static string Issue(string signingKey, TimeSpan? lifetime = null, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new ArgumentException("A signing key is required to issue CSRF tokens.", nameof(signingKey));
        }

        var issuedAt = now ?? DateTimeOffset.UtcNow;
        var expiry = issuedAt.Add(lifetime ?? Lifetime).ToUnixTimeSeconds();
        var nonce = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var signature = Sign(signingKey, $"{nonce}.{expiry}");

        return $"{nonce}.{expiry}.{signature}";
    }

    /// <summary>
    /// Validates the token shape, HMAC and expiry. Never throws: malformed,
    /// tampered, expired or wrong-key tokens all return <c>false</c>.
    /// </summary>
    public static bool IsValid(string? token, string? signingKey, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(signingKey))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var (nonce, expiryText, signature) = (parts[0], parts[1], parts[2]);
        if (string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        if (!long.TryParse(expiryText, out var expiryUnix))
        {
            return false;
        }

        var reference = now ?? DateTimeOffset.UtcNow;
        if (expiryUnix <= reference.ToUnixTimeSeconds())
        {
            return false;
        }

        var expected = Sign(signingKey, $"{nonce}.{expiryText}");

        return FixedTimeEquals(signature, expected);
    }

    /// <summary>
    /// Double-submit check: the echoed candidate must equal the cookie value
    /// and the value must be a live, correctly signed token. Comparing first
    /// keeps a leaked-but-valid token unusable without the matching cookie.
    /// </summary>
    public static bool IsMatch(string? cookieToken, string? candidateToken, string? signingKey, DateTimeOffset? now = null)
        => !string.IsNullOrWhiteSpace(candidateToken)
            && string.Equals(cookieToken, candidateToken, StringComparison.Ordinal)
            && IsValid(candidateToken, signingKey, now);

    /// <summary>
    /// Options for the readable CSRF cookie: JavaScript must read it to echo
    /// the token (so <c>HttpOnly</c> is off by design — the value carries no
    /// authority by itself), while <c>Secure</c> + <c>SameSite=Lax</c> +
    /// <c>Path=/</c> match the session cookies.
    /// </summary>
    public static CookieOptions BuildCookieOptions() => new()
    {
        HttpOnly = false,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = Lifetime,
        Expires = DateTimeOffset.UtcNow.Add(Lifetime)
    };

    /// <summary>Plants the issued token in the readable CSRF cookie.</summary>
    public static void AppendCsrfCookie(HttpResponse response, string token)
        => response.Cookies.Append(CookieName, token, BuildCookieOptions());

    private static string Sign(string signingKey, string data)
    {
        var key = Encoding.UTF8.GetBytes(signingKey);
        var payload = Encoding.ASCII.GetBytes(data);

        return Base64UrlEncode(HMACSHA256.HashData(key, payload));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);

        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
