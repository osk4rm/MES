using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint-scoped integration tests for the observability slice (issue
/// #252): the Prometheus scrape endpoint (200 with exposition content when
/// enabled, 404 when disabled), OTLP no-op boot, and the tenant-read auth
/// regression guard. The <c>X-Correlation-ID</c> echo contract itself is
/// covered by <c>CorrelationIdEndpointTests</c> (issue #251); these tests
/// only assert the echoed id is present alongside observability behaviour.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ObservabilityEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CorrelationHeader = "X-Correlation-ID";

    [Fact]
    public async Task MetricsEndpoint_WhenDisabled_Returns404()
    {
        // Arrange - the shared host runs with PrometheusEnabled=false.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MetricsEndpoint_WhenEnabled_ReturnsPrometheusExposition()
    {
        // Arrange - an isolated host with the scrape endpoint enabled via the
        // documented environment override. The suite runs sequentially (the
        // Integration collection disables parallelization), so process-level
        // env manipulation is safe as long as it is reverted.
        Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", "true");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var client = factory.CreateClient();

            // Act - hit a tenant endpoint first so request metrics exist.
            await client.GetAsync("/health/live");
            var response = await client.GetAsync("/metrics");

            // Assert - Prometheus exposition content type plus both the
            // request duration histogram and its count series. The ASP.NET
            // Core hosting meter includes the unit infix (_seconds).
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("http_server_request_duration_seconds_bucket");
            body.Should().Contain("http_server_request_duration_seconds_count");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", null);
        }
    }

    [Fact]
    public async Task Host_WithOtlpEndpointConfigured_BootsAndServesTraffic()
    {
        // Arrange - an isolated host with OTLP export pointed at a local
        // collector address. Proves the app boots with tracing in export mode
        // and serves traffic with no startup exception.
        Environment.SetEnvironmentVariable("Observability__OtlpEndpoint", "http://localhost:4317");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health/live");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            Guid.TryParse(GetCorrelationId(response), out _).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__OtlpEndpoint", null);
        }
    }

    [Fact]
    public async Task AuthenticatedTenantRead_WithObservabilityEnabled_Returns200()
    {
        // Arrange
        using var client = await Fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert - no 401 regression with the middleware and SDK enabled.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Guid.TryParse(GetCorrelationId(response), out _).Should().BeTrue();
    }

    [Fact]
    public async Task AnonymousSignIn_WithObservabilityEnabled_ReturnsTokens()
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/auth/sign-in",
            new { email = TestData.IntegrationTestData.AdminEmail, password = TestData.IntegrationTestData.AdminPassword });

        // Assert - no auth regression with the middleware and SDK enabled.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Guid.TryParse(GetCorrelationId(response), out _).Should().BeTrue();
    }

    private static string GetCorrelationId(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(CorrelationHeader, out var values).Should().BeTrue();
        return values!.Single();
    }
}
