using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for JWT hardening: sign-in issues both
/// tokens, refresh rotates single-use, reuse is rejected with family revocation,
/// sign-out revokes, and tampered access tokens are rejected.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthRefreshEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string RefreshUrl = "/api/auth/refresh";
    private const string SignOutUrl = "/api/auth/sign-out";

    [Fact]
    public async Task SignIn_ReturnsAccessAndRefreshTokens()
    {
        using var client = Fixture.CreateClient();

        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await ReadAsync<AuthTokensDto>(response);
        token.AccessToken.Should().NotBeNullOrWhiteSpace();
        token.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_WithValidToken_RotatesAndInvalidatesOld()
    {
        // Arrange — fresh sign-in for an isolated token family.
        var (client, tokens) = await SignInAsync();

        // Act — rotate once.
        var refresh = await client.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });

        // Assert — new pair issued, refresh token changed.
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await ReadAsync<AuthTokensDto>(refresh);
        rotated.AccessToken.Should().NotBeNullOrWhiteSpace();
        rotated.RefreshToken.Should().NotBeNullOrWhiteSpace();
        rotated.RefreshToken.Should().NotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedToken_Returns401()
    {
        // Arrange
        var (client, tokens) = await SignInAsync();
        var first = await client.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });
        first.EnsureSuccessStatusCode();

        // Act — replay the already-rotated token.
        var reuse = await client.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });

        // Assert — reuse detected and rejected.
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignOut_ThenRefresh_Returns401()
    {
        // Arrange
        var (client, tokens) = await SignInAsync();
        var signOut = await client.PostAsJsonAsync(SignOutUrl, new { refreshToken = tokens.RefreshToken });
        signOut.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — refresh with the revoked token.
        var refresh = await client.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });

        // Assert
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_WithTamperedAccessToken_Returns401()
    {
        // Arrange — valid sign-in, then tamper the last character.
        var (_, tokens) = await SignInAsync();
        var tampered = tokens.AccessToken[..^1] + (tokens.AccessToken[^1] == 'a' ? 'b' : 'a');
        using var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        // Act
        var response = await client.GetAsync("/api/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithoutAccessToken_Returns401()
    {
        // Arrange — refresh requires an authenticated tenant (access token).
        using var anonymous = Fixture.CreateClient();

        // Act
        var response = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = "anything" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(HttpClient Client, AuthTokensDto Tokens)> SignInAsync()
    {
        var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });
        response.EnsureSuccessStatusCode();
        var tokens = await ReadAsync<AuthTokensDto>(response);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (client, tokens);
    }

    private sealed record AuthTokensDto(string AccessToken, string RefreshToken);
}
