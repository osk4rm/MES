using System.Net;
using AsistOff.MES.Shared.Infrastructure.Auth;
using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Regression guard for the CI failure on PR #353 (issue #351): the global
/// fallback authorization policy challenged anonymous Prometheus scrapes with
/// 401 because <c>/metrics</c> was a metadata-free middleware branch behind
/// <c>UseAuthorization</c>. <c>MapMesObservability</c> maps the scrape
/// endpoint with an explicit <c>AllowAnonymous()</c> opt-out, so anonymous
/// scrapes stay reachable when enabled (200, Prometheus exposition) and keep
/// the documented 404 when disabled, while the fallback still 401s unmarked
/// endpoints. Real Kestrel host with the real <c>AddAuth</c> wiring, no mocks.
/// </summary>
public sealed class MetricsScrapeAnonymousTests
{
    [Fact]
    public async Task MetricsScrape_WhenEnabled_AnonymousReturnsPrometheusExposition()
    {
        // Arrange — real host with the production auth wiring (fallback policy active).
        await using var app = BuildHost(prometheusEnabled: true);
        await app.StartAsync();
        try
        {
            using var client = CreateClient(app);

            // A protected probe proves the fallback policy is enforced here.
            var protectedResponse = await client.GetAsync("/protected-probe");
            protectedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Act — anonymous Prometheus scrape.
            var response = await client.GetAsync("/metrics");

            // Assert — reachable without credentials (401 without the AllowAnonymous opt-out).
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task MetricsScrape_WhenDisabled_AnonymousReturns404InsteadOfChallenge()
    {
        // Arrange — real host with the scrape endpoint disabled.
        await using var app = BuildHost(prometheusEnabled: false);
        await app.StartAsync();
        try
        {
            using var client = CreateClient(app);

            // The fallback policy is still enforced for unmarked endpoints.
            var protectedResponse = await client.GetAsync("/protected-probe");
            protectedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Act — anonymous GET on the disabled scrape path.
            var response = await client.GetAsync("/metrics");

            // Assert — the documented disabled-means-404 contract holds (an
            // unmapped path would surface a 401 challenge under the fallback).
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication BuildHost(bool prometheusEnabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["auth:IssuerSigningKey"] = new string('k', 40),
            ["auth:Issuer"] = "AsistOff.MES.Tests",
            ["auth:Audience"] = "AsistOff.MES.Tests",
            ["Observability:ServiceName"] = "AsistOff.MES.Tests",
            ["Observability:ServiceVersion"] = "1.0.0-test",
            ["Observability:SamplingRatio"] = "0",
            ["Observability:PrometheusEnabled"] = prometheusEnabled ? "true" : "false",
        });
        builder.Services.AddAuth();
        builder.Services.AddMesObservability(builder.Configuration);
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/protected-probe", () => Results.Ok("ok")).RequireAuthorization();
        app.MapMesObservability();
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
