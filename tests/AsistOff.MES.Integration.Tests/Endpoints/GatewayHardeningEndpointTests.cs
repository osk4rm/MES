using System.Net;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Gateway hardening (issue #369): HSTS follows the browser-facing scheme —
/// present with forwarded HTTPS through a trusted proxy, absent over plain
/// HTTP — and credentialed CORS never reflects an arbitrary origin.
/// The shared host runs in Development with the shipped
/// <c>appsettings.Development.json</c> allowlist (loopback Vite origins), so
/// a listed loopback origin succeeds while <c>https://evil.test</c> gets no
/// allow-origin header. Startup fail-fast paths (wildcard
/// <c>AllowedHosts</c> outside Development, empty
/// <c>cors:allowedOrigins</c> outside Development) throw before the host
/// serves traffic and are covered by unit tests on
/// <c>HostFilteringGuard</c>/<c>DevCorsPolicy</c>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class GatewayHardeningEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task ForwardedHttpsThroughTrustedProxy_CarriesStrictTransportSecurity()
    {
        // Arrange - host trusting the proxy peer: the TCP source is the proxy
        // and X-Forwarded-Proto: https travels from the TLS-terminating proxy
        // (as shipped in nginx.conf). ForwardedHeaders rewrites the scheme
        // before SecurityHeadersMiddleware runs.
        const string proxyIp = "172.18.0.5";
        using var factory = new MesWebApplicationFactory(
            Fixture.PostgresConnectionString,
            configureTrustedProxies: trusted => trusted.KnownProxies = [proxyIp]);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(TestRemoteIpStartupFilter.HeaderName, proxyIp);
        request.Headers.Add("X-Forwarded-Proto", "https");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var hsts = response.Headers.GetValues("Strict-Transport-Security").Should().ContainSingle().Subject;
        hsts.Should().Contain("max-age=31536000");
        hsts.Should().Contain("includeSubDomains");
    }

    [Fact]
    public async Task PlainHttpWithoutForwardedHttps_CarriesNoStrictTransportSecurity()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().NotContain(h => h.Key == "Strict-Transport-Security");
    }

    [Fact]
    public async Task SpoofedForwardedProtoWithoutTrustedProxy_CarriesNoStrictTransportSecurity()
    {
        // Arrange - default-deny: no trusted proxy entry, so the forwarded
        // proto header is ignored and the scheme stays http (fail-closed).
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Forwarded-Proto", "https");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().NotContain(h => h.Key == "Strict-Transport-Security");
    }

    [Fact]
    public async Task Preflight_FromEvilOrigin_ReturnsNoAllowOriginHeader()
    {
        // Arrange - the hardened Development CORS path must not reflect an
        // arbitrary origin with credentials (issue #369).
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "https://evil.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.Should().NotContain(h => h.Key == "Access-Control-Allow-Origin");
    }

    [Fact]
    public async Task Preflight_FromConfiguredLoopbackOrigin_Succeeds()
    {
        // Arrange - a configured loopback origin (shipped Development
        // allowlist) still succeeds with credentials.
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be("http://localhost:5173");
    }
}
