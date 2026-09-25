using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// Endpoint integration tests for OpenTelemetry traces and metrics (issue #252):
/// the effective <c>X-Correlation-ID</c> is echoed on every response including
/// <c>/metrics</c>, W3C <c>traceparent</c> propagation never breaks the
/// pipeline, <c>/metrics</c> serves Prometheus exposition only when enabled,
/// and authenticated tenant reads keep working with observability enabled.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ObservabilityEndpointTests(MesApplicationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CorrelationHeader = "X-Correlation-ID";

    [Fact]
    public async Task Get_WithoutCorrelationHeader_ReturnsEchoedGuid()
    {
        // Arrange - anonymous client, no correlation or traceparent headers.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echoed = GetCorrelationHeader(response);
        echoed.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(echoed!, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Get_WithValidCorrelationAndTraceparent_EchoesCorrelation()
    {
        // Arrange - W3C traceparent must propagate without breaking the
        // correlation echo or the request itself.
        using var client = Fixture.CreateClient();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationHeader, correlationId);
        request.Headers.Add("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        GetCorrelationHeader(response).Should().Be(correlationId);
    }

    [Fact]
    public async Task Metrics_Disabled_Returns404()
    {
        // Arrange - shared host uses appsettings.Development.json where
        // Observability:PrometheusEnabled is false.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Metrics_Enabled_ReturnsPrometheusExposition()
    {
        // Arrange - isolated host with the Prometheus scrape endpoint on.
        using var factory = new MesWebApplicationFactory(
            Fixture.PostgresConnectionString,
            configOverrides: new Dictionary<string, string?>
            {
                ["Observability:PrometheusEnabled"] = "true",
            });
        using var client = factory.CreateClient();

        // Act - first hit generates request-duration samples, second scrapes them.
        _ = await client.GetAsync("/health/live");
        var response = await client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Contain("text/plain");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("http_server_request_duration");
        GetCorrelationHeader(response).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AuthenticatedTenantRead_WithObservability_Returns200()
    {
        // Arrange - tenant read keeps working with the correlation bridge and
        // OTel instrumentation enabled; anonymous sign-in still issues tokens.
        using var client = await Fixture.CreateAuthenticatedClientAsync();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products?page=1&pageSize=1");
        request.Headers.Add(CorrelationHeader, correlationId);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(CorrelationHeader).Single().Should().Be(correlationId);
    }

    [Fact]
    public async Task OtlpConfigured_AppBootsAndServesTenantTraffic()
    {
        // Arrange - OTLP pointed at a dummy collector (no listener): the app
        // must still boot with no startup exception and serve tenant reads.
        // Span export itself is verified against a live collector in e2e.
        using var factory = new MesWebApplicationFactory(
            Fixture.PostgresConnectionString,
            configOverrides: new Dictionary<string, string?>
            {
                ["Observability:OtlpEndpoint"] = "http://localhost:4317",
            });
        using var anonymous = factory.CreateClient();

        // Act - anonymous probe proves the host booted.
        var live = await anonymous.GetAsync("/health/live");

        // Assert
        live.StatusCode.Should().Be(HttpStatusCode.OK);
        Guid.TryParse(GetCorrelationHeader(live)!, out _).Should().BeTrue();

        // Act - authenticated tenant read through the OTLP-configured host.
        using var authenticated = await SignInAsync(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products?page=1&pageSize=1");
        var correlationId = Guid.NewGuid().ToString();
        request.Headers.Add(CorrelationHeader, correlationId);
        var products = await authenticated.SendAsync(request);

        // Assert
        products.StatusCode.Should().Be(HttpStatusCode.OK);
        products.Headers.GetValues(CorrelationHeader).Single().Should().Be(correlationId);
    }

    private static async Task<HttpClient> SignInAsync(MesWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/sign-in",
            new { email = IntegrationTestData.AdminEmail, password = IntegrationTestData.AdminPassword });

        response.EnsureSuccessStatusCode();

        var accessToken = AuthCookieHelper.GetAccessToken(response);
        accessToken.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    private static string? GetCorrelationHeader(HttpResponseMessage response)
        => response.Headers.TryGetValues(CorrelationHeader, out var values) ? values.SingleOrDefault() : null;
}
