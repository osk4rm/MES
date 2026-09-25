using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Shared.Infrastructure.Auth;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for cookie-transport auth (#241):
/// <c>HttpOnly</c> <c>Secure</c> <c>SameSite=Lax</c> cookies on sign-in and
/// refresh, cookie-only reads, rotation via cookie with replay rejection,
/// sign-out clearing plus revocation, cross-tenant refresh rejection, and
/// continued <c>Authorization</c> header support during transition.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthCookieEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string RefreshUrl = "/api/auth/refresh";
    private const string SignOutUrl = "/api/auth/sign-out";
    private const string ProductsUrl = "/api/products";

    [Fact]
    public async Task CookieOnlyRead_Succeeds_AndAnonymousRead_FailsWith401()
    {
        // Arrange
        var (access, refresh) = await SignInCookiesAsync();

        // Act — products read presenting only cookies, no Authorization header.
        using var cookieClient = Fixture.CreateClient();
        var ok = await cookieClient.SendAsync(GetWithCookies(ProductsUrl, access, refresh));

        // Assert
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        using var anonymous = Fixture.CreateClient();
        var denied = await anonymous.GetAsync(ProductsUrl);
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HeaderCaller_WithTokenFromCookie_Succeeds()
    {
        // Arrange — transition: the access token replayed as a header still works.
        var (access, _) = await SignInCookiesAsync();
        using var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);

        // Act
        var response = await client.GetAsync(ProductsUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithRefreshCookie_RotatesSetsFreshCookies_AndReplayFailsWith401()
    {
        // Arrange
        var (access, refresh) = await SignInCookiesAsync();

        // Act — rotate presenting only the refresh cookie (empty JSON body).
        using var client = Fixture.CreateClient();
        var rotated = await client.SendAsync(PostWithCookies(RefreshUrl, access, refresh));

        // Assert — fresh pair issued with new values.
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAccess = MesApplicationFixture.ExtractCookie(rotated, AuthCookies.AccessCookieName);
        var newRefresh = MesApplicationFixture.ExtractCookie(rotated, AuthCookies.RefreshCookieName);
        newAccess.Should().NotBeNullOrWhiteSpace();
        newRefresh.Should().NotBeNullOrWhiteSpace();
        newRefresh.Should().NotBe(refresh);

        var raw = await rotated.Content.ReadAsStringAsync();
        raw.Should().NotContain("accessToken");
        raw.Should().NotContain("refreshToken");

        // Act — replay the already-rotated refresh cookie.
        using var replay = Fixture.CreateClient();
        var denied = await replay.SendAsync(PostWithCookies(RefreshUrl, access, refresh));

        // Assert — reuse rejected, while the rotated access cookie still reads.
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var rotatedClient = Fixture.CreateClient();
        var probe = await rotatedClient.SendAsync(GetWithCookies(ProductsUrl, newAccess, newRefresh));
        probe.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithBodyToken_StillWorksDuringTransition()
    {
        // Arrange — header-era clients sending the token in the body keep working.
        var (_, refresh) = await SignInCookiesAsync();
        using var anonymous = Fixture.CreateClient();

        // Act
        var response = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = refresh });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithCookieAndSameOriginHeader_Rotates()
    {
        // Arrange — same-origin browser callers pass the Origin check.
        var (access, refresh) = await SignInCookiesAsync();
        using var client = Fixture.CreateClient();
        var request = PostWithCookies(RefreshUrl, access, refresh);
        request.Headers.Add("Origin", "http://localhost");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignOut_WithCookiesOnly_ClearsCookies_AndRevokesRefresh()
    {
        // Arrange — sign out authenticated by the access cookie alone.
        var (access, refresh) = await SignInCookiesAsync();

        // Act
        using var client = Fixture.CreateClient();
        var signOut = await client.SendAsync(PostWithCookies(SignOutUrl, access, refresh));

        // Assert — 204 with both cookies expired.
        signOut.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cleared = signOut.Headers.GetValues("Set-Cookie").ToList();
        cleared.Should().Contain(h =>
            h.StartsWith(AuthCookies.AccessCookieName + "=") && h.Contains("1970"));
        cleared.Should().Contain(h =>
            h.StartsWith(AuthCookies.RefreshCookieName + "=") && h.Contains("1970"));

        // Act — refresh with the revoked cookie.
        using var anonymous = Fixture.CreateClient();
        var retry = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = refresh });

        // Assert
        retry.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_CrossTenantAmbientCookieCombination_Returns401()
    {
        // Arrange — refresh cookie issued under tenant A, access cookie from tenant B.
        var (_, refreshA) = await SignInCookiesAsync();
        var (emailB, passwordB) = await Fixture.CreateTenantAsync();
        var (accessB, _) = await SignInCookiesAsync(emailB, passwordB);

        // Act
        using var attacker = Fixture.CreateClient();
        var refresh = await attacker.SendAsync(PostWithCookies(RefreshUrl, accessB, refreshA));

        // Assert — a refresh cookie from tenant A cannot mint a session for a
        // tenant B caller, while anonymous rotation of tenant A's cookie works.
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var anonymous = Fixture.CreateClient();
        var retry = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = refreshA });
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<(string Access, string Refresh)> SignInCookiesAsync(
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
            MesApplicationFixture.ExtractCookie(response, AuthCookies.AccessCookieName),
            MesApplicationFixture.ExtractCookie(response, AuthCookies.RefreshCookieName));
    }

    private static HttpRequestMessage GetWithCookies(string url, string? access, string? refresh)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", BuildCookieHeader(access, refresh));
        return request;
    }

    private static HttpRequestMessage PostWithCookies(string url, string? access, string? refresh)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("Cookie", BuildCookieHeader(access, refresh));
        return request;
    }

    private static string BuildCookieHeader(string? access, string? refresh)
        => $"{AuthCookies.AccessCookieName}={access}; {AuthCookies.RefreshCookieName}={refresh}";
}
