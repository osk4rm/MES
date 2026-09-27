using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using AsistOff.MES.Shared.Infrastructure.Protection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for Gateway abuse protection (issue #235,
/// hardened by issue #323): default security headers on API responses, the
/// minimal anonymous tenant signup payload, and per-IP fixed-window throttling
/// of the anonymous bootstrap endpoints with <c>429 + Retry-After</c>.
///
/// Issue #323: the throttle partitions by <c>Connection.RemoteIpAddress</c>
/// only — spoofed <c>X-Forwarded-For</c> never changes the partition.
/// <c>X-Test-Remote-Ip</c> (see <see cref="TestRemoteIpStartupFilter"/>) sets
/// the TCP source deterministically per request; forwarded headers are only
/// honored when the host explicitly trusts the proxy via
/// <see cref="TrustedProxyOptions"/> (real Forwarded Headers middleware).
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
    public async Task SignIn_RotatingForwardedHeader_DoesNotEscapeThrottle()
    {
        // Arrange - isolated host with a tiny budget and default-deny trusted
        // proxies (untrusted direct connection): one TCP source rotating
        // X-Forwarded-For must still throttle as a single client.
        using var factory = CreateThrottledFactory();
        using var client = factory.CreateClient();
        var tcpSource = UniqueTestIp();

        // Act - two attempts consume the budget (invalid credentials -> 401,
        // which still counts), each with a different spoofed header; the
        // third with yet another spoofed value must still be throttled.
        for (var i = 0; i < 2; i++)
        {
            var attempt = await PostSignInAsync(client, tcpSource, UniqueTestIp(), $"nobody-{Guid.NewGuid():N}@integration.local");
            attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var throttled = await PostSignInAsync(client, tcpSource, UniqueTestIp(), $"nobody-{Guid.NewGuid():N}@integration.local");

        // Assert - 401 (bad credentials) vs 429 (throttled) contract holds
        // even under header rotation.
        throttled.StatusCode.Should().Be((HttpStatusCode)429);
        throttled.Headers.Should().Contain(h => h.Key == "Retry-After");
    }

    [Fact]
    public async Task SignIn_DistinctRemoteIps_GetIndependentBudgets()
    {
        // Arrange - same isolated host, two distinct TCP sources (no
        // forwarded headers at all): each gets its own budget.
        using var factory = CreateThrottledFactory();
        using var client = factory.CreateClient();
        var throttledIp = UniqueTestIp();
        var otherIp = UniqueTestIp();

        // Act - exhaust the first client's budget (401s still count), then
        // prove the second client is unaffected and the first stays throttled.
        for (var i = 0; i < 2; i++)
        {
            var attempt = await PostSignInAsync(client, throttledIp, forwardedFor: null, email: $"nobody-{Guid.NewGuid():N}@integration.local");
            attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var throttled = await PostSignInAsync(client, throttledIp, forwardedFor: null, email: $"nobody-{Guid.NewGuid():N}@integration.local");

        var unaffected = await PostSignInAsync(client, otherIp, forwardedFor: null, email: $"nobody-{Guid.NewGuid():N}@integration.local");

        // Assert
        throttled.StatusCode.Should().Be((HttpStatusCode)429);
        throttled.Headers.Should().Contain(h => h.Key == "Retry-After");
        unaffected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignIn_TrustedProxy_HonorsForwardedClientIp()
    {
        // Arrange - host trusting the nginx proxy peer: the TCP source is the
        // proxy, the real client travels in X-Forwarded-For (as shipped in
        // nginx.conf via $proxy_add_x_forwarded_for). Distinct forwarded IPs
        // behind the same proxy must get independent budgets.
        const string proxyIp = "172.18.0.5";
        using var factory = CreateThrottledFactory(trusted => trusted.KnownProxies = [proxyIp]);
        using var client = factory.CreateClient();
        var clientA = $"203.0.113.{Random.Shared.Next(10, 200)}";
        var clientB = $"203.0.113.{Random.Shared.Next(201, 250)}";

        // Act - exhaust client A through the proxy (401s still count), prove
        // client B through the same proxy is unaffected, and client A stays
        // throttled.
        for (var i = 0; i < 2; i++)
        {
            var attempt = await PostSignInAsync(client, proxyIp, clientA, $"nobody-{Guid.NewGuid():N}@integration.local");
            attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var unaffected = await PostSignInAsync(client, proxyIp, clientB, $"nobody-{Guid.NewGuid():N}@integration.local");

        var throttled = await PostSignInAsync(client, proxyIp, clientA, $"nobody-{Guid.NewGuid():N}@integration.local");

        // Assert
        unaffected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        throttled.StatusCode.Should().Be((HttpStatusCode)429);
        throttled.Headers.Should().Contain(h => h.Key == "Retry-After");
    }

    [Fact]
    public async Task SignIn_HappyPath_UnderBudget_Succeeds()
    {
        // Arrange - isolated host with a generous budget; valid dev credentials
        // must succeed (200) and never be throttled under budget.
        using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString, protection =>
        {
            protection.SignIn.PermitLimit = 10;
            protection.SignIn.WindowSeconds = 60;
        });
        using var client = factory.CreateClient();

        // Act
        var response = await PostSignInAsync(
            client,
            UniqueTestIp(),
            forwardedFor: null,
            email: IntegrationTestData.AdminEmail,
            password: IntegrationTestData.AdminPassword);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TenantCreate_ExceedingPerIpLimit_Returns429()
    {
        // Arrange - same TCP source, no forwarded headers: two creates consume
        // the budget (happy path under budget still succeeds with 201), the
        // third is throttled.
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

    private MesWebApplicationFactory CreateThrottledFactory(Action<TrustedProxyOptions>? configureTrusted = null) =>
        new(Fixture.PostgresConnectionString, protection =>
        {
            protection.SignIn.PermitLimit = 2;
            protection.SignIn.WindowSeconds = 60;
            protection.TenantCreate.PermitLimit = 2;
            protection.TenantCreate.WindowSeconds = 60;
        }, configureTrusted);

    private static string UniqueTestIp()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return $"10.{bytes[0]}.{bytes[1]}.{bytes[2]}";
    }

    private static async Task<HttpResponseMessage> PostSignInAsync(
        HttpClient client,
        string remoteIp,
        string? forwardedFor,
        string email,
        string password = "not-the-password")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, remoteIp);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostTenantAsync(HttpClient client, string remoteIp)
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
        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, remoteIp);

        return await client.SendAsync(request);
    }
}
