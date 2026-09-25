using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the anonymous
/// <c>POST /api/auth/sign-in</c> endpoint. The session travels over httpOnly
/// auth cookies (<c>mes_access</c> / <c>mes_refresh</c>); the body carries no
/// usable token strings.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthenticationEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string BaseUrl = "/api/auth/sign-in";

    [Fact]
    public async Task SignIn_WithValidCredentials_SetsAuthCookies()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthCookieHelper.GetAccessToken(response).Should().NotBeNullOrWhiteSpace();
        AuthCookieHelper.GetRefreshToken(response).Should().NotBeNullOrWhiteSpace();

        var body = await ReadAsync<JsonWebTokenDto>(response);
        body.AccessToken.Should().BeNullOrEmpty("the body must not carry usable token strings for storage");
    }

    [Fact]
    public async Task SignIn_WithWrongPassword_Returns401WithoutCookies()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = "definitely-not-the-password"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AuthCookieHelper.GetSetCookies(response).Should().BeEmpty("failed sign-in must not issue a session");
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

    private sealed record JsonWebTokenDto(string? AccessToken);
}
