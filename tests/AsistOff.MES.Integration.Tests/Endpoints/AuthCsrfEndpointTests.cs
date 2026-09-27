using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the double-submit CSRF gate
/// (issue #376): <c>GET /api/auth/csrf</c> issuance, 403 with no rotation
/// or clearing when the echoed token is missing or mismatched on refresh
/// and sign-out, the JSON body-field fallback, the Origin-guard precedence,
/// and the unchanged sign-in happy/401 paths.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthCsrfEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string RefreshUrl = "/api/auth/refresh";
    private const string SignOutUrl = "/api/auth/sign-out";
    private const string CsrfUrl = "/api/auth/csrf";
    private const string ProductsUrl = "/api/products";

    [Fact]
    public async Task CsrfIssuance_SetsCookieAndReturnsMatchingToken()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync(CsrfUrl);

        // Assert — anonymous issuance: 200, readable mes_csrf cookie whose
        // value equals the body token.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadAsync<CsrfDto>(response);
        body.CsrfToken.Should().NotBeNullOrWhiteSpace();
        AuthCookieHelper.GetCookieValue(response, AuthCookieHelper.CsrfCookieName)
            .Should().Be(body.CsrfToken);
    }

    [Fact]
    public async Task Refresh_WithoutCsrfToken_Returns403AndLeavesSessionUsable()
    {
        // Arrange — live session presented only as cookies, no CSRF pair.
        var cookies = await SignInCookiesAsync();
        using var victim = AuthCookieHelper.CreateCookieClient(Fixture, cookies);

        // Act — forged cross-site shape: ambient cookies, no token.
        var forged = await victim.PostAsJsonAsync(RefreshUrl, new { });

        // Assert — rejected before the handler: 403 and nothing rotated.
        forged.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AuthCookieHelper.GetSetCookies(forged).Should().BeEmpty("a rejected refresh must rotate nothing");

        // Act — the same refresh cookie still rotates with a valid ticket.
        await AuthCookieHelper.AttachCsrfAsync(victim);
        var legitimate = await victim.PostAsJsonAsync(RefreshUrl, new { });

        // Assert — the session survived the forged call.
        legitimate.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthCookieHelper.GetRefreshToken(legitimate).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_WithMismatchedCsrfToken_Returns403()
    {
        // Arrange — two live tickets; cookie carries A, header echoes B.
        var cookies = await SignInCookiesAsync();
        using var ticketClient = Fixture.CreateClient();
        var ticketA = await AuthCookieHelper.GetCsrfAsync(ticketClient);
        var ticketB = await AuthCookieHelper.GetCsrfAsync(ticketClient);
        ticketA.Token.Should().NotBe(ticketB.Token);

        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, RefreshUrl)
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("Cookie", $"{cookies}; {ticketA.CookiePair}");
        request.Headers.Add(AuthCookieHelper.CsrfHeaderName, ticketB.Token);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AuthCookieHelper.GetSetCookies(response).Should().BeEmpty();
    }

    [Fact]
    public async Task Refresh_WithValidToken_Rotates()
    {
        // Arrange — Origin-less non-browser shape with a valid ticket.
        var cookies = await SignInCookiesAsync();
        var oldRefresh = CookieValue(cookies, AuthCookieHelper.RefreshCookieName);
        using var client = AuthCookieHelper.CreateCookieClient(Fixture, cookies);
        await AuthCookieHelper.AttachCsrfAsync(client);

        // Act
        var refresh = await client.PostAsJsonAsync(RefreshUrl, new { });

        // Assert — 200 with a rotated pair.
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var newRefresh = AuthCookieHelper.GetRefreshToken(refresh);
        newRefresh.Should().NotBeNullOrWhiteSpace();
        newRefresh.Should().NotBe(oldRefresh);

        // Act — the rotated access cookie authenticates reads.
        using var rotated = AuthCookieHelper.CreateCookieClient(
            Fixture,
            AuthCookieHelper.BuildCookieHeader(AuthCookieHelper.GetAccessToken(refresh), newRefresh));
        var probe = await rotated.GetAsync(ProductsUrl);

        // Assert
        probe.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithTokenInBodyField_Succeeds()
    {
        // Arrange — non-browser caller echoing the token as a JSON field.
        var cookies = await SignInCookiesAsync();
        using var ticketClient = Fixture.CreateClient();
        var ticket = await AuthCookieHelper.GetCsrfAsync(ticketClient);
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, RefreshUrl)
        {
            Content = JsonContent.Create(new { csrfToken = ticket.Token })
        };
        request.Headers.Add("Cookie", $"{cookies}; {ticket.CookiePair}");

        // Act — no X-CSRF-Token header, field only.
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithCrossOriginHeader_Returns403()
    {
        // Arrange — valid ticket but a cross-host Origin: the baseline guard
        // fires first and nothing rotates.
        var cookies = await SignInCookiesAsync();
        using var ticketClient = Fixture.CreateClient();
        var ticket = await AuthCookieHelper.GetCsrfAsync(ticketClient);
        using var sender = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, RefreshUrl)
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("Cookie", $"{cookies}; {ticket.CookiePair}");
        request.Headers.Add(AuthCookieHelper.CsrfHeaderName, ticket.Token);
        request.Headers.Add("Origin", "https://evil.example");

        // Act
        var response = await sender.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AuthCookieHelper.GetSetCookies(response).Should().BeEmpty();
    }

    [Fact]
    public async Task SignOut_WithoutCsrfToken_Returns403AndKeepsSession()
    {
        // Arrange — authenticated purely by cookies, no CSRF pair.
        var cookies = await SignInCookiesAsync();
        using var victim = AuthCookieHelper.CreateCookieClient(Fixture, cookies);

        // Act — forged sign-out with ambient cookies only.
        var forged = await victim.PostAsJsonAsync(SignOutUrl, new { });

        // Assert — rejected before the handler: 403 and nothing cleared.
        forged.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AuthCookieHelper.GetSetCookies(forged).Should().BeEmpty("a rejected sign-out must clear nothing");

        // Act — the session is still alive with a valid ticket.
        await AuthCookieHelper.AttachCsrfAsync(victim);
        var refresh = await victim.PostAsJsonAsync(RefreshUrl, new { });

        // Assert
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignOut_WithValidToken_ClearsCookies()
    {
        // Arrange
        var cookies = await SignInCookiesAsync();
        using var client = AuthCookieHelper.CreateCookieClient(Fixture, cookies);
        await AuthCookieHelper.AttachCsrfAsync(client);

        // Act
        var signOut = await client.PostAsJsonAsync(SignOutUrl, new { });

        // Assert — 204 with both cookies expired.
        signOut.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cleared = AuthCookieHelper.GetSetCookies(signOut);
        cleared.Should().Contain(c => c.StartsWith($"{AuthCookieHelper.AccessCookieName}=", StringComparison.Ordinal));
        cleared.Should().Contain(c => c.StartsWith($"{AuthCookieHelper.RefreshCookieName}=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignIn_WithoutCsrfToken_Returns403AndIssuesNothing()
    {
        // Arrange — no ticket at all.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        // Assert — rejected before the handler: no session issued.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AuthCookieHelper.GetSetCookies(response).Should().BeEmpty();
    }

    [Fact]
    public async Task SignIn_WithValidToken_Succeeds()
    {
        // Arrange
        using var client = Fixture.CreateClient();
        await AuthCookieHelper.AttachCsrfAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        // Assert — happy path keeps working with the token wired through.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthCookieHelper.GetAccessToken(response).Should().NotBeNullOrWhiteSpace();
        AuthCookieHelper.GetRefreshToken(response).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SignIn_WithValidTokenButWrongPassword_Returns401()
    {
        // Arrange
        using var client = Fixture.CreateClient();
        await AuthCookieHelper.AttachCsrfAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = "definitely-not-the-password"
        });

        // Assert — past the gate, the credential check still owns the 401.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AuthCookieHelper.GetSetCookies(response).Should().BeEmpty();
    }

    private async Task<string> SignInCookiesAsync()
    {
        using var client = Fixture.CreateClient();
        await AuthCookieHelper.AttachCsrfAsync(client);
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
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

    private sealed record CsrfDto(string? CsrfToken);
}
