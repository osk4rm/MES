using System.Net;
using System.Net.Http.Json;
using AsistOff.MES.Integration.Tests.Infrastructure;
using AsistOff.MES.Integration.Tests.TestData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Integration.Tests.Endpoints;

/// <summary>
/// HTTP-edge default-deny guard for issue #351 (security audit 2026-09-26,
/// Low): the production host must enforce the global fallback authorization
/// policy (unauthenticated requests to endpoints without
/// <c>[AllowAnonymous]</c> are rejected with 401), while the documented
/// anonymous surface — sign-in, tenant lookup, health probes, the Prometheus
/// scrape endpoint, and the <c>/error</c> path — keeps working unchanged.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class FallbackAuthorizationEndpointTests(MesApplicationFixture fixture)
    : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task ProductionHost_RegistersFallbackPolicyRequiringAuthenticatedUser()
    {
        // Arrange — the real host booted by the collection fixture.

        // Act
        using var scope = Fixture.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallback = await provider.GetFallbackPolicyAsync();

        // Assert — fails without the AddAuth fallback registration, so this is
        // the test that proves the MVC layer (not just ApiController's
        // [Authorize]) fails closed for metadata-free endpoints.
        fallback.Should().NotBeNull("endpoints without explicit auth metadata must fail closed");
        fallback!.Requirements.OfType<DenyAnonymousAuthorizationRequirement>()
            .Should().ContainSingle("the fallback policy must require an authenticated user");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        // Arrange — anonymous client, no token.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products?page=1&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ErrorPath_Anonymous_ReturnsSanitizedProblemInsteadOfChallenge()
    {
        // Arrange — anonymous client, no token.
        using var client = Fixture.CreateClient();

        // Act — /error carries no exception feature outside the handler
        // re-execution path, so it renders the generic sanitized envelope.
        var response = await client.GetAsync("/error");

        // Assert — reachable (no 401 challenge) with the sanitized problem body.
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AnonymousSignIn_StillWorks()
    {
        // Arrange — the [AllowAnonymous] sign-in action must survive the fallback.
        using var client = Fixture.CreateClient();
        await AuthCookieHelper.AttachCsrfAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new
        {
            email = IntegrationTestData.AdminEmail,
            password = IntegrationTestData.AdminPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthCookieHelper.GetAccessToken(response).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AnonymousTenantLookup_StillWorks()
    {
        // Arrange — the [AllowAnonymous] tenant lookup must survive the fallback.
        // An unknown id proves anonymous reachability without provisioning data.
        using var client = Fixture.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/tenants/{Guid.NewGuid()}");

        // Assert — routed past authorization to the handler (404, never 401).
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HealthProbes_StillWorkWithoutAuthentication()
    {
        // Arrange — the AllowAnonymous() probe mappings must survive the fallback.
        using var client = Fixture.CreateClient();

        // Act
        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        // Assert
        live.StatusCode.Should().Be(HttpStatusCode.OK);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MetricsScrape_WhenEnabled_AnonymousReturnsPrometheusExposition()
    {
        // Arrange — an isolated Prometheus-enabled host (the shared host runs
        // with PrometheusEnabled=false). Scrapers call without credentials, so
        // the AllowAnonymous() scrape mapping must survive the fallback.
        Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", "true");
        try
        {
            await using var factory = new MesWebApplicationFactory(Fixture.PostgresConnectionString);
            using var client = factory.CreateClient();

            // Act — hit a probe first so request metrics exist, then scrape anonymously.
            await client.GetAsync("/health/live");
            var response = await client.GetAsync("/metrics");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__PrometheusEnabled", null);
        }
    }
}
