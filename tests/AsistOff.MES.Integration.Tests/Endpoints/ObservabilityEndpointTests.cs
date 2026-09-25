using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Shared.Infrastructure.Correlation;
using AsistOff.MES.Shared.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    public async Task TenantGet_WithOtlpConfigured_EmitsServerSpanWithTraceparentAndTenant()
    {
        // Arrange - an isolated host with OTLP export enabled (issue #252
        // AC1): the app must boot in export mode and serve an authenticated
        // tenant read whose real server span keeps the upstream W3C trace and
        // carries the correlation + tenant tags with the AsistOff.MES export
        // identity. The suite runs sequentially, so the process-level
        // ActivityListener and env override below are reverted afterwards.
        Environment.SetEnvironmentVariable("Observability__OtlpEndpoint", "http://localhost:4317");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var client = factory.CreateClient();

            // Sign in on the isolated host (same seeded database) and present
            // the session as a Bearer header, mirroring MesApplicationFixture.
            using var signIn = await client.PostAsJsonAsync(
                "/api/auth/sign-in",
                new { email = TestData.IntegrationTestData.AdminEmail, password = TestData.IntegrationTestData.AdminPassword });
            signIn.EnsureSuccessStatusCode();
            var accessToken = AuthCookieHelper.GetAccessToken(signIn);
            accessToken.Should().NotBeNullOrWhiteSpace();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var stopped = new ConcurrentBag<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => stopped.Add(activity),
            };
            ActivitySource.AddActivityListener(listener);
            try
            {
                var correlationId = Guid.NewGuid().ToString();
                var upstreamTraceId = ActivityTraceId.CreateRandom().ToString();
                var upstreamSpanId = ActivitySpanId.CreateRandom().ToString();

                // Act - a real authenticated tenant read carrying an upstream
                // W3C traceparent (set explicitly: the in-process TestServer
                // transport performs no ambient propagation of its own).
                using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products");
                request.Headers.Add(CorrelationHeader, correlationId);
                request.Headers.Add("traceparent", $"00-{upstreamTraceId}-{upstreamSpanId}-01");
                using var response = await client.SendAsync(request);

                // Assert - no 401 regression with export enabled, echo intact.
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                GetCorrelationId(response).Should().Be(correlationId);

                // The real server span for this request keeps the upstream
                // trace and carries the correlation + tenant tags.
                var serverSpan = stopped
                    .Where(a => a.Kind == ActivityKind.Server
                        && Equals(a.GetTagItem(CorrelationIds.ActivityTagKey), correlationId))
                    .Should().ContainSingle().Subject;
                serverSpan.TraceId.ToString().Should().Be(upstreamTraceId);
                var tenantTag = serverSpan.GetTagItem(TenantTraceEnricher.TenantTagKey);
                tenantTag.Should().NotBeNull();
                Guid.TryParse(tenantTag!.ToString(), out var spanTenant).Should().BeTrue();
                spanTenant.Should().NotBe(Guid.Empty);
            }
            finally
            {
                listener.Dispose();
            }

            // The export identity of this exact host: the resource the
            // gateway registration builds from these options (proven to carry
            // service.name by ObservabilityRegistrationTests) travels on
            // every exported span.
            var options = factory.Services.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
            options.ServiceName.Should().Be("AsistOff.MES");
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
