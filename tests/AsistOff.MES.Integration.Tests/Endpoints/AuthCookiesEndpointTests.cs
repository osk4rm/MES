using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the httpOnly cookie transport
/// (issue #241, slice 1 of 2): sign-in issues hardened cookies with no usable
/// body tokens, cookie-only callers authenticate, refresh rotates via the
/// refresh cookie with replay rejection, sign-out clears cookies and revokes,
/// cross-tenant refresh stays bound to the issuing tenant, header callers
/// keep working, and cross-origin cookie writes are rejected.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthCookiesEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string RefreshUrl = "/api/auth/refresh";
    private const string SignOutUrl = "/api/auth/sign-out";
    private const string ProductsUrl = "/api/products";

    [Fact]
    public async Task CookieOnly_GetProducts_Returns200()
    {
        // Arrange — session presented only as cookies, no Authorization header.
        var cookies = await SignInCookiesAsync();
        using var client = AuthCookieHelper.CreateCookieClient(Fixture, cookies);

        // Act
        var response = await client.GetAsync(ProductsUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProducts_WithoutCookies_Returns401()
    {
        // Arrange — anonymous client, no cookies and no header.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync(ProductsUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HeaderBearer_FromCookieSession_StillWorks()
    {
        // Arrange — header transport fed from the access cookie during transition.
        var cookies = await SignInCookiesAsync();
        var access = CookieValue(cookies, AuthCookieHelper.AccessCookieName);
        using var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        // Act
        var response = await client.GetAsync(ProductsUrl);

        // Assert — no regression for header callers.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_ViaCookie_RotatesAndSetsFreshCookies()
    {
        // Arrange — refresh cookie only, empty body, no Authorization header.
        var cookies = await SignInCookiesAsync();
        var oldRefresh = CookieValue(cookies, AuthCookieHelper.RefreshCookieName);
        using var anonymous = AuthCookieHelper.CreateCookieClient(
            Fixture, AuthCookieHelper.BuildCookieHeader(accessToken: null, refreshToken: oldRefresh));

        // Act
        var refresh = await anonymous.PostAsJsonAsync(RefreshUrl, new { });

        // Assert — rotated pair issued over fresh cookies.
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAccess = AuthCookieHelper.GetAccessToken(refresh);
        var newRefresh = AuthCookieHelper.GetRefreshToken(refresh);
        newAccess.Should().NotBeNullOrWhiteSpace();
        newRefresh.Should().NotBeNullOrWhiteSpace();
        newRefresh.Should().NotBe(oldRefresh);
    }

    [Fact]
    public async Task Refresh_ReplayViaCookie_Returns401()
    {
        // Arrange — rotate once via cookie.
        var cookies = await SignInCookiesAsync();
        var refreshToken = CookieValue(cookies, AuthCookieHelper.RefreshCookieName);
        using var anonymous = AuthCookieHelper.CreateCookieClient(
            Fixture, AuthCookieHelper.BuildCookieHeader(accessToken: null, refreshToken: refreshToken));
        var first = await anonymous.PostAsJsonAsync(RefreshUrl, new { });
        first.EnsureSuccessStatusCode();

        // Act — replay the consumed refresh cookie.
        using var replay = AuthCookieHelper.CreateCookieClient(
            Fixture, AuthCookieHelper.BuildCookieHeader(accessToken: null, refreshToken: refreshToken));
        var reuse = await replay.PostAsJsonAsync(RefreshUrl, new { });

        // Assert — the used token is invalidated.
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithEmptyBodyAndNoCookie_Returns401()
    {
        // Arrange — anonymous client, no cookies and no body token.
        using var client = Fixture.CreateClient();

        // Act — empty body with no refresh cookie: absent token, not a 400.
        var refresh = await client.PostAsJsonAsync(RefreshUrl, new { });

        // Assert — the handler rejects the missing token with 401.
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithExplicitBlankToken_Returns400()
    {
        // Arrange — anonymous client, no cookies, explicitly blank body token.
        using var client = Fixture.CreateClient();

        // Act
        var refresh = await client.PostAsJsonAsync(RefreshUrl, new { refreshToken = "" });

        // Assert — an explicitly provided blank token is a malformed request.
        refresh.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignOut_ViaCookies_ClearsCookiesAndRevokes()
    {
        // Arrange — authenticated purely by cookies.
        var cookies = await SignInCookiesAsync();
        var refreshToken = CookieValue(cookies, AuthCookieHelper.RefreshCookieName);
        using var client = AuthCookieHelper.CreateCookieClient(Fixture, cookies);

        // Act — sign out with an empty body; the refresh cookie selects the token.
        var signOut = await client.PostAsJsonAsync(SignOutUrl, new { });

        // Assert — 204 with both cookies expired.
        signOut.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cleared = AuthCookieHelper.GetSetCookies(signOut);
        cleared.Should().Contain(c => c.StartsWith($"{AuthCookieHelper.AccessCookieName}=", StringComparison.Ordinal)
            && IsExpiredSetCookie(c));
        cleared.Should().Contain(c => c.StartsWith($"{AuthCookieHelper.RefreshCookieName}=", StringComparison.Ordinal)
            && IsExpiredSetCookie(c));

        // Act — the revoked refresh cookie no longer rotates.
        using var anonymous = AuthCookieHelper.CreateCookieClient(
            Fixture, AuthCookieHelper.BuildCookieHeader(accessToken: null, refreshToken: refreshToken));
        var refresh = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken });

        // Assert
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static bool IsExpiredSetCookie(string setCookie)
        => setCookie.Contains("1970", StringComparison.OrdinalIgnoreCase)
            || setCookie.Contains("max-age=0", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task Refresh_CrossTenantCookie_StaysBoundToIssuingTenant()
    {
        // Arrange — tenant A's refresh cookie presented alongside tenant B's session.
        var cookiesA = await SignInCookiesAsync();
        var refreshA = CookieValue(cookiesA, AuthCookieHelper.RefreshCookieName);
        var tenantA = ReadClaim(CookieValue(cookiesA, AuthCookieHelper.AccessCookieName), "tenant_id");
        var (emailB, passwordB) = await Fixture.CreateTenantAsync();
        var cookiesB = await SignInCookiesAsync(emailB, passwordB);
        var tenantB = ReadClaim(CookieValue(cookiesB, AuthCookieHelper.AccessCookieName), "tenant_id");
        tenantA.Should().NotBe(tenantB);

        using var attacker = AuthCookieHelper.CreateCookieClient(Fixture,
            AuthCookieHelper.BuildCookieHeader(
                CookieValue(cookiesB, AuthCookieHelper.AccessCookieName), refreshA));

        // Act
        var refresh = await attacker.PostAsJsonAsync(RefreshUrl, new { });

        // Assert — the minted session stays bound to tenant A, never tenant B.
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        ReadClaim(AuthCookieHelper.GetAccessToken(refresh)!, "tenant_id").Should().Be(tenantA);
    }

    [Fact]
    public async Task SignIn_WithCrossOriginHeader_Returns403()
    {
        // Arrange — CSRF probe: Origin host differs from the request host.
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, SignInUrl)
        {
            Content = JsonContent.Create(new
            {
                email = IntegrationTestData.AdminEmail,
                password = IntegrationTestData.AdminPassword
            })
        };
        request.Headers.Add("Origin", "https://evil.example");

        // Act
        var response = await client.SendAsync(request);

        // Assert — cookie write rejected, no session issued.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AuthCookieHelper.GetSetCookies(response).Should().BeEmpty();
    }

    private async Task<string> SignInCookiesAsync(string? email = null, string? password = null)
    {
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = email ?? IntegrationTestData.AdminEmail,
            password = password ?? IntegrationTestData.AdminPassword
        });
        response.EnsureSuccessStatusCode();

        var cookies = AuthCookieHelper.BuildCookieHeader(response);
        cookies.Should().NotBeNullOrWhiteSpace();
        return cookies;
    }

    private static string CookieValue(string cookieHeader, string name)
    {
        var value = cookieHeader.Split(';')
            .Select(p => p.Trim())
            .FirstOrDefault(p => p.StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))
            ?[(name.Length + 1)..];
        value.Should().NotBeNullOrWhiteSpace();
        return value!;
    }

    private static string ReadClaim(string jwt, string claimType)
        => ReadAllClaims(jwt).First(c => c.Type == claimType).Value;

    private static List<Claim> ReadAllClaims(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var json = Encoding.UTF8.GetString(Base64UrlDecode(payload));
        var result = new List<Claim>();
        using var doc = JsonDocument.Parse(json);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    result.Add(new Claim(property.Name, item.GetString() ?? string.Empty));
                }
            }
            else
            {
                result.Add(new Claim(property.Name, property.Value.ToString()));
            }
        }

        return result;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
