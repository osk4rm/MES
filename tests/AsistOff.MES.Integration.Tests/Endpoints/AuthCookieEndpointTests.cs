using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the httpOnly cookie transport
/// (issue #241, slice 1 of 2): sign-in sets cookies and sanitizes the body,
/// cookie-only reads succeed, refresh rotates via cookie with replay rejection,
/// sign-out clears cookies and revokes, cross-tenant refresh stays bound, and
/// Authorization-header callers keep working during transition.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthCookieEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string RefreshUrl = "/api/auth/refresh";
    private const string SignOutUrl = "/api/auth/sign-out";
    private const string ProductsUrl = "/api/products";

    [Fact]
    public async Task SignIn_SetsAccessAndRefreshCookies_WithRequiredFlags()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var setCookies = CookieTestHelpers.GetSetCookies(response);
        var access = setCookies.FirstOrDefault(c =>
            c.StartsWith("mes_access=", StringComparison.OrdinalIgnoreCase));
        var refresh = setCookies.FirstOrDefault(c =>
            c.StartsWith("mes_refresh=", StringComparison.OrdinalIgnoreCase));
        access.Should().NotBeNull();
        refresh.Should().NotBeNull();

        foreach (var cookie in new[] { access!, refresh! })
        {
            cookie.ToLowerInvariant().Should().Contain("httponly");
            cookie.ToLowerInvariant().Should().Contain("secure");
            cookie.Should().ContainEquivalentOf("SameSite=Lax");
            cookie.Should().ContainEquivalentOf("Path=/");
        }
    }

    [Fact]
    public async Task CookieOnly_Read_Succeeds_WithoutAuthorizationHeader()
    {
        var access = await SignInAccessCookieAsync();
        using var client = Fixture.CreateClient();
        CookieTestHelpers.SetCookies(client, ("mes_access", access));

        var response = await client.GetAsync(ProductsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Read_WithoutCookiesOrHeader_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.GetAsync(ProductsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ViaCookie_Rotates_ReplayFailsWith401()
    {
        var refreshToken = await SignInRefreshCookieAsync();
        using var anonymous = Fixture.CreateClient();
        CookieTestHelpers.SetCookies(anonymous, ("mes_refresh", refreshToken));

        // Empty body: the refresh cookie supplies the token.
        var refresh = await anonymous.PostAsJsonAsync(RefreshUrl, new { });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotatedAccess = CookieTestHelpers.GetCookieValue(refresh, "mes_access");
        var rotatedRefresh = CookieTestHelpers.GetCookieValue(refresh, "mes_refresh");
        rotatedAccess.Should().NotBeNullOrWhiteSpace();
        rotatedRefresh.Should().NotBeNullOrWhiteSpace();
        rotatedRefresh.Should().NotBe(refreshToken);

        var sanitized = await ReadAsync<AuthTokensDto>(refresh);
        sanitized.AccessToken.Should().BeNullOrWhiteSpace();
        sanitized.RefreshToken.Should().BeNullOrWhiteSpace();

        // Replay the used cookie: rotation invalidated it.
        using var replay = Fixture.CreateClient();
        CookieTestHelpers.SetCookies(replay, ("mes_refresh", refreshToken));
        var reuse = await replay.PostAsJsonAsync(RefreshUrl, new { });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ViaBody_StillWorks_DuringTransition()
    {
        var refreshToken = await SignInRefreshCookieAsync();
        using var anonymous = Fixture.CreateClient();

        var refresh = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken });

        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        CookieTestHelpers.GetCookieValue(refresh, "mes_access").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SignOut_ClearsCookies_AndRevokes_SoRefreshFailsWith401()
    {
        var (access, refreshToken) = await SignInCookiePairAsync();
        using var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        CookieTestHelpers.SetCookies(client, ("mes_refresh", refreshToken));

        // Empty body: the refresh cookie selects the token to revoke.
        var signOut = await client.PostAsJsonAsync(SignOutUrl, new { });
        signOut.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cleared = CookieTestHelpers.GetSetCookies(signOut);
        cleared.Should().Contain(c => c.StartsWith("mes_access=", StringComparison.OrdinalIgnoreCase)
            && c.ToLowerInvariant().Contains("expires="));
        cleared.Should().Contain(c => c.StartsWith("mes_refresh=", StringComparison.OrdinalIgnoreCase)
            && c.ToLowerInvariant().Contains("expires="));

        using var anonymous = Fixture.CreateClient();
        var refresh = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CrossTenant_RefreshCookie_CannotMintOtherTenantSession()
    {
        var (accessA, refreshA) = await SignInCookiePairAsync();
        var tenantA = ReadClaim(accessA, "tenant_id");
        var (emailB, passwordB) = await Fixture.CreateTenantAsync();
        var (accessB, _) = await SignInCookiePairAsync(emailB, passwordB);
        var tenantB = ReadClaim(accessB, "tenant_id");
        tenantA.Should().NotBe(tenantB);

        // Attacker presents tenant B's session but tenant A's refresh cookie.
        using var attacker = Fixture.CreateClient();
        attacker.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessB);
        CookieTestHelpers.SetCookies(attacker, ("mes_refresh", refreshA));

        var refresh = await attacker.PostAsJsonAsync(RefreshUrl, new { });

        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        ReadClaim(CookieTestHelpers.GetCookieValue(refresh, "mes_access"), "tenant_id")
            .Should().Be(tenantA);
    }

    [Fact]
    public async Task HeaderBased_Caller_WithCookieAccessToken_StillSucceeds()
    {
        var access = await SignInAccessCookieAsync();
        using var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var response = await client.GetAsync(ProductsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignIn_WithCrossOriginHeader_IsRejected()
    {
        using var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://evil.example.com");

        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<string> SignInAccessCookieAsync(string? email = null, string? password = null)
    {
        var (access, _) = await SignInCookiePairAsync(email, password);
        return access;
    }

    private async Task<string> SignInRefreshCookieAsync()
    {
        var (_, refresh) = await SignInCookiePairAsync();
        return refresh;
    }

    private async Task<(string Access, string Refresh)> SignInCookiePairAsync(
        string? email = null, string? password = null)
    {
        using var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = email ?? IntegrationTestData.AdminEmail,
            password = password ?? IntegrationTestData.AdminPassword
        });
        response.EnsureSuccessStatusCode();
        return (
            CookieTestHelpers.GetCookieValue(response, "mes_access"),
            CookieTestHelpers.GetCookieValue(response, "mes_refresh"));
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

    private sealed record AuthTokensDto(string AccessToken, string RefreshToken);
}
