using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Verifies the OpenTelemetry registration: no-op mode without an OTLP
/// endpoint, full export mode with endpoint + Prometheus, and no startup
/// exception in either case.
/// </summary>
public class ObservabilityRegistrationTests
{
    [Fact]
    public void AddMesObservability_WithoutOtlpEndpoint_RegistersNoOpProviders()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildConfiguration("""
            { "Observability": { "PrometheusEnabled": false } }
            """);

        // Act
        var act = () => services.AddMesObservability(configuration);

        // Assert
        act.Should().NotThrow();
        using var provider = services.BuildServiceProvider();
        provider.GetService<TracerProvider>().Should().NotBeNull();
        provider.GetService<MeterProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddMesObservability_WithOtlpEndpointAndPrometheus_RegistersProviders()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildConfiguration("""
            {
              "Observability": {
                "ServiceName": "AsistOff.MES",
                "OtlpEndpoint": "http://localhost:4317",
                "SamplingRatio": 0.5,
                "PrometheusEnabled": true
              }
            }
            """);

        // Act
        var act = () => services.AddMesObservability(configuration);

        // Assert
        act.Should().NotThrow();
        using var provider = services.BuildServiceProvider();
        provider.GetService<TracerProvider>().Should().NotBeNull();
        provider.GetService<MeterProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddMesObservability_MalformedOtlpEndpoint_FallsBackToNoOp()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildConfiguration("""
            { "Observability": { "OtlpEndpoint": "not-a-uri" } }
            """);

        // Act
        var act = () => services.AddMesObservability(configuration);

        // Assert
        act.Should().NotThrow();
        using var provider = services.BuildServiceProvider();
        provider.GetService<TracerProvider>().Should().NotBeNull();
    }

    private static IConfiguration BuildConfiguration(string json) =>
        new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
}
