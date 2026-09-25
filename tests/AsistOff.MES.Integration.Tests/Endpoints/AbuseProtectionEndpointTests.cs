using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for Gateway abuse protection (issue #235):
/// default security headers on API responses, the minimal anonymous tenant
/// signup payload, and per-IP fixed-window throttling of the anonymous
/// bootstrap endpoints with <c>429 + Retry-After</c>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AbuseProtectionEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task ApiResponse_CarriesDefaultSecurityHeaders()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Contain("default-src 'self'");
    }

    [Fact]
    public async Task AuthenticatedRead_CarriesSecurityHeaders_AndStillReturns200()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/reason-codes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task HttpsApiResponse_CarriesStrictTransportSecurity()
    {
        // Arrange - TestServer honors the absolute request URI scheme, so an
        // https URI exercises the TLS-only HSTS branch deterministically.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("https://localhost/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle()
            .Which.Should().Contain("max-age=31536000");
    }

    [Fact]
    public async Task TenantCreate_ReturnsOnlyMinimalAnonymousPayload()
    {
        // Arrange
        using var client = Fixture.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // Act
        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"abuse-{suffix}",
            displayName = $"Abuse Tenant {suffix}",
            contactEmail = $"abuse-{suffix}@integration.local",
            settings = string.Empty,
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        document.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("id", "name", "isActive");

        var lowered = body.ToLowerInvariant();
        foreach (var fragment in new[] { "secret", "token", "connectionstring", "password", "contactemail", "displayname", "settings" })
        {
            lowered.Should().NotContain(fragment, $"anonymous payload must not expose '{fragment}'");
        }
    }

    [Fact]
    public async Task SignIn_ExceedingPerIpLimit_Returns429WithRetryAfter_WhileOtherIpIsUnaffected()
    {
        // Arrange - isolated host with a tiny budget so the test needs only a
        // handful of requests; unique spoofed client IPs keep it hermetic.
        using var factory = CreateThrottledFactory();
        using var client = factory.CreateClient();
        var throttledIp = UniqueTestIp();
        var otherIp = UniqueTestIp();

        // Act - two attempts consume the budget (invalid credentials -> 401,
        // which still counts as an attempt), the third must be throttled.
        for (var i = 0; i < 2; i++)
        {
            var attempt = await PostSignInAsync(client, throttledIp, $"nobody-{Guid.NewGuid():N}@integration.local");
            attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var throttled = await PostSignInAsync(client, throttledIp, $"nobody-{Guid.NewGuid():N}@integration.local");

        var unaffected = await PostSignInAsync(client, otherIp, $"nobody-{Guid.NewGuid():N}@integration.local");

        // Assert
        throttled.StatusCode.Should().Be((HttpStatusCode)429);
        throttled.Headers.Should().Contain(h => h.Key == "Retry-After");
        unaffected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TenantCreate_ExceedingPerIpLimit_Returns429()
    {
        // Arrange
        using var factory = CreateThrottledFactory();
        using var client = factory.CreateClient();
        var throttledIp = UniqueTestIp();

        // Act
        for (var i = 0; i < 2; i++)
        {
            var created = await PostTenantAsync(client, throttledIp);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var throttled = await PostTenantAsync(client, throttledIp);

        // Assert
        throttled.StatusCode.Should().Be((HttpStatusCode)429);
        throttled.Headers.Should().Contain(h => h.Key == "Retry-After");
    }

    private MesWebApplicationFactory CreateThrottledFactory() =>
        new(Fixture.PostgresConnectionString, protection =>
        {
            protection.SignIn.PermitLimit = 2;
            protection.SignIn.WindowSeconds = 60;
            protection.TenantCreate.PermitLimit = 2;
            protection.TenantCreate.WindowSeconds = 60;
        });

    private static string UniqueTestIp()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return $"10.{bytes[0]}.{bytes[1]}.{bytes[2]}";
    }

    private static async Task<HttpResponseMessage> PostSignInAsync(HttpClient client, string clientIp, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new { email, password = "not-the-password" })
        };
        request.Headers.Add("X-Forwarded-For", clientIp);

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostTenantAsync(HttpClient client, string clientIp)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new
            {
                name = $"throttle-{suffix}",
                displayName = $"Throttle Tenant {suffix}",
                contactEmail = $"throttle-{suffix}@integration.local",
                settings = string.Empty,
                password = "Passw0rd!",
                confirmPassword = "Passw0rd!"
            })
        };
        request.Headers.Add("X-Forwarded-For", clientIp);

        return await client.SendAsync(request);
    }
}
