using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the Gateway health probes (issue
/// #249): <c>/health/live</c> answers without authentication or database
/// access, <c>/health/ready</c> reflects PostgreSQL reachability, the
/// historical <c>/health</c> stays a readiness alias, and no probe is ever
/// rejected with 401 or throttled with 429 under sign-in burst traffic.
///
/// The 503-when-unreachable contract (problem details without secret leaks)
/// is proven at unit level (<c>PostgresReadinessHealthCheckTests</c>,
/// <c>HealthProbeResponseWriterTests</c>): a full host cannot boot against a
/// dead database because startup migrations would fail first.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class HealthProbeEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Live_Returns200_WithoutAuthentication()
    {
        // Arrange - anonymous client, no token.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Healthy");
    }

    [Fact]
    public async Task Ready_Returns200_WhenDatabaseReachable()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthAlias_MatchesReadyStatus()
    {
        // Arrange - the historical endpoint must keep working for existing
        // callers and reflect readiness, not liveness wiring.
        using var client = Fixture.CreateClient();

        // Act
        var alias = await client.GetAsync("/health");
        var ready = await client.GetAsync("/health/ready");

        // Assert
        alias.StatusCode.Should().Be(HttpStatusCode.OK);
        alias.StatusCode.Should().Be(ready.StatusCode);
        (await alias.Content.ReadAsStringAsync()).Should().Be(await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Probes_AreNeverRejectedOrThrottled_UnderSignInBurstTraffic()
    {
        // Arrange - isolated host with a tiny sign-in budget; the spoofed IP
        // burns through it (invalid credentials -> 401 still counts), and the
        // next sign-in attempt is throttled with 429.
        using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString, protection =>
        {
            protection.SignIn.PermitLimit = 2;
            protection.SignIn.WindowSeconds = 60;
        });
        using var client = factory.CreateClient();
        var burstIp = $"10.{Guid.NewGuid().ToByteArray()[0]}.{Guid.NewGuid().ToByteArray()[0]}.7";

        for (var i = 0; i < 2; i++)
        {
            (await PostSignInAsync(client, burstIp)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await PostSignInAsync(client, burstIp)).StatusCode.Should().Be((HttpStatusCode)429);

        // Act - the same client IP polls every probe without credentials.
        var live = await GetProbeAsync(client, burstIp, "/health/live");
        var ready = await GetProbeAsync(client, burstIp, "/health/ready");
        var alias = await GetProbeAsync(client, burstIp, "/health");

        // Assert - probes are anonymous (never 401) and rate-limit exempt
        // (never 429), even while sign-in traffic from the same IP is throttled.
        live.StatusCode.Should().Be(HttpStatusCode.OK);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        alias.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> PostSignInAsync(HttpClient client, string clientIp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new
            {
                email = $"nobody-{Guid.NewGuid():N}@integration.local",
                password = "not-the-password"
            })
        };
        request.Headers.Add("X-Forwarded-For", clientIp);

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetProbeAsync(HttpClient client, string clientIp, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-For", clientIp);

        return await client.SendAsync(request);
    }
}
