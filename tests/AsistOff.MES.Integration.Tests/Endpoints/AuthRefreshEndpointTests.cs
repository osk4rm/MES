using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Shared.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for JWT hardening (#232): audience-validated
/// access tokens, anonymous refresh rotation with replay/revocation rejection and
/// tenant binding of refresh tokens.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthRefreshEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string SignInUrl = "/api/auth/sign-in";
    private const string RefreshUrl = "/api/auth/refresh";
    private const string SignOutUrl = "/api/auth/sign-out";
    private const string ProtectedUrl = "/api/roles";

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
        // Arrange — fresh sign-in for an isolated token family. The refresh call
        // itself is anonymous (no bearer), modelling an expired access token.
        var (_, tokens) = await SignInAsync();
        using var anonymous = Fixture.CreateClient();

        // Act — rotate once.
        var refresh = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });

        // Assert — new pair issued, refresh token changed, access token usable.
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await ReadAsync<AuthTokensDto>(refresh);
        rotated.AccessToken.Should().NotBeNullOrWhiteSpace();
        rotated.RefreshToken.Should().NotBeNullOrWhiteSpace();
        rotated.RefreshToken.Should().NotBe(tokens.RefreshToken);

        using var rotatedClient = Fixture.CreateClient();
        rotatedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", rotated.AccessToken);
        var probe = await rotatedClient.GetAsync(ProtectedUrl);
        probe.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithoutAccessToken_Succeeds()
    {
        // Arrange — refresh is anonymous by design so sessions survive access expiry.
        var (_, tokens) = await SignInAsync();
        using var anonymous = Fixture.CreateClient();

        // Act
        var response = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedToken_Returns401()
    {
        // Arrange
        var (_, tokens) = await SignInAsync();
        using var anonymous = Fixture.CreateClient();
        var first = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });
        first.EnsureSuccessStatusCode();

        // Act — replay the already-rotated token.
        var reuse = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });

        // Assert — reuse detected and rejected.
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignOut_ThenRefresh_Returns401()
    {
        // Arrange — revocation itself stays authenticated; the refresh is anonymous.
        var (client, tokens) = await SignInAsync();
        var signOut = await client.PostAsJsonAsync(SignOutUrl, new { refreshToken = tokens.RefreshToken });
        signOut.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — refresh with the revoked token.
        using var anonymous = Fixture.CreateClient();
        var refresh = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });

        // Assert
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401()
    {
        // Arrange
        using var anonymous = Fixture.CreateClient();

        // Act
        var response = await anonymous.PostAsJsonAsync(RefreshUrl, new { refreshToken = "unknown-opaque-token" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMismatchedAudience_Returns401()
    {
        // Arrange — same key, issuer and claims as a valid token, only aud differs.
        var (_, tokens) = await SignInAsync();
        var forged = MintTokenWithAudience(tokens.AccessToken, "attacker-audience");
        using var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        // Act
        var response = await client.GetAsync(ProtectedUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMissingAudience_Returns401()
    {
        // Arrange — valid signature and claims, but no aud claim at all.
        var (_, tokens) = await SignInAsync();
        var forged = MintTokenWithAudience(tokens.AccessToken, audience: null);
        using var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        // Act
        var response = await client.GetAsync(ProtectedUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        var response = await client.GetAsync(ProtectedUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithTenantAToken_CannotMintTenantBSession()
    {
        // Arrange — two tenants; the attacker holds tenant B's access token and
        // tenant A's refresh token.
        var (_, tokensA) = await SignInAsync();
        var tenantA = ReadClaim(tokensA.AccessToken, "tenant_id");
        var (emailB, passwordB) = await Fixture.CreateTenantAsync();
        var (_, tokensB) = await SignInAsync(emailB, passwordB);
        var tenantB = ReadClaim(tokensB.AccessToken, "tenant_id");
        tenantA.Should().NotBe(tenantB);

        using var attacker = Fixture.CreateClient();
        attacker.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokensB.AccessToken);

        // Act — refresh tenant A's token while presenting tenant B's session.
        var refresh = await attacker.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokensA.RefreshToken });

        // Assert — the minted session stays bound to tenant A, never tenant B.
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await ReadAsync<AuthTokensDto>(refresh);
        ReadClaim(rotated.AccessToken, "tenant_id").Should().Be(tenantA);
    }

    private async Task<(HttpClient Client, AuthTokensDto Tokens)> SignInAsync(
        string? email = null, string? password = null)
    {
        var client = Fixture.CreateClient();
        var response = await client.PostAsJsonAsync(SignInUrl, new
        {
            email = email ?? IntegrationTestData.AdminEmail,
            password = password ?? IntegrationTestData.AdminPassword
        });
        response.EnsureSuccessStatusCode();
        var tokens = await ReadAsync<AuthTokensDto>(response);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (client, tokens);
    }

    /// <summary>
    /// Re-signs the claims of a valid access token with the host's own key and
    /// issuer but a caller-chosen audience (or none), so audience validation is
    /// the only thing under test.
    /// </summary>
    private string MintTokenWithAudience(string validAccessToken, string? audience)
    {
        var auth = Fixture.Services.GetRequiredService<AuthOptions>();
        var claims = ReadAllClaims(validAccessToken)
            .Where(c => c.Type is not "aud")
            .Select(c => new Claim(c.Type, c.Value))
            .ToList();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.IssuerSigningKey!));
        var jwt = new JwtSecurityToken(
            issuer: auth.Issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
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
