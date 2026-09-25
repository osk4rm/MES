using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Shared.Infrastructure.Auth;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the anonymous
/// <c>POST /api/auth/sign-in</c> endpoint (cookie transport, #241).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthenticationEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/auth/sign-in";

    [Fact]
    public async Task SignIn_WithValidCredentials_SetsHardenedCookies_AndBodyCarriesNoTokens()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var access = MesApplicationFixture.ExtractCookie(response, AuthCookies.AccessCookieName);
        access.Should().NotBeNullOrWhiteSpace();
        var refresh = MesApplicationFixture.ExtractCookie(response, AuthCookies.RefreshCookieName);
        refresh.Should().NotBeNullOrWhiteSpace();
        refresh.Should().NotBe(access);

        // Hardened cookie attributes on the wire.
        var accessHeader = response.Headers.GetValues("Set-Cookie")
            .Should().ContainSingle(h => h.StartsWith(AuthCookies.AccessCookieName + "=")).Subject;
        accessHeader.Should().ContainEquivalentOf("httponly");
        accessHeader.Should().ContainEquivalentOf("secure");
        accessHeader.Should().ContainEquivalentOf("samesite=lax");
        accessHeader.Should().ContainEquivalentOf("path=/");

        // The body carries session metadata only — no usable token strings.
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("accessToken");
        raw.Should().NotContain("refreshToken");
        raw.Should().NotContain(access);
    }

    [Fact]
    public async Task SignIn_WithWrongPassword_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = "definitely-not-the-password"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignIn_WithUnknownEmail_Returns401()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            email = $"nobody-{Guid.NewGuid():N}@integration.local",
            password = "whatever"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignIn_WithForeignOrigin_Returns400()
    {
        // Arrange — CSRF backstop: SameSite=Lax plus an Origin check.
        using var client = Fixture.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
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

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
