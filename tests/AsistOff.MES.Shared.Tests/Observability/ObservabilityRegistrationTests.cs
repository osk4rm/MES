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

    [Fact]
    public void BuildResource_CarriesConfiguredServiceIdentity()
    {
        // Arrange
        var options = new ObservabilityOptions { ServiceName = "AsistOff.MES", ServiceVersion = "2.1.0" };

        // Act
        var resource = ObservabilityRegistration.BuildResourceBuilder(options).Build();

        // Assert - the service identity exported with every span and metric.
        var attributes = resource.Attributes.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        attributes.Should().ContainKey("service.name").WhoseValue.Should().Be("AsistOff.MES");
        attributes.Should().ContainKey("service.version").WhoseValue.Should().Be("2.1.0");
    }

    [Fact]
    public void BuildResource_Defaults_ToProductServiceName()
    {
        // Arrange + Act
        var resource = ObservabilityRegistration.BuildResourceBuilder(new ObservabilityOptions()).Build();

        // Assert - every span carries service.name AsistOff.MES (issue #252 AC1).
        var attributes = resource.Attributes.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        attributes.Should().ContainKey("service.name").WhoseValue.Should().Be("AsistOff.MES");
    }

    [Fact]
    public void BuildSampler_ReturnsParentBasedSampler()
    {
        // Arrange
        var options = new ObservabilityOptions { SamplingRatio = 0.5 };

        // Act
        var sampler = ObservabilityRegistration.BuildSampler(options);

        // Assert - parent-based so an upstream W3C traceparent decision wins;
        // the root ratio itself is clamped by GetSamplingRatio (tested in
        // ObservabilityOptionsTests).
        sampler.Should().BeOfType<ParentBasedSampler>();
    }

    private static IConfiguration BuildConfiguration(string json) =>
        new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
}
