using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Verifies the <c>Observability</c> configuration contract: safe defaults
/// (no export, no scrape endpoint), binding of the OTLP endpoint / service
/// identity / sampling ratio / Prometheus flag, and sampling-ratio bounds.
/// </summary>
public class ObservabilityOptionsTests
{
    [Fact]
    public void Defaults_DisableExport_AndKeepFullSampling()
    {
        // Arrange + Act
        var options = new ObservabilityOptions();

        // Assert
        options.ServiceName.Should().Be("AsistOff.MES");
        options.ServiceVersion.Should().NotBeNullOrWhiteSpace();
        options.OtlpEndpoint.Should().BeEmpty();
        options.HasOtlpEndpoint.Should().BeFalse();
        options.SamplingRatio.Should().Be(1.0);
        options.GetSamplingRatio().Should().Be(1.0);
        options.PrometheusEnabled.Should().BeFalse();
    }

    [Fact]
    public void Bind_FromObservabilitySection_AppliesAllValues()
    {
        // Arrange
        var configuration = BuildConfiguration("""
            {
              "Observability": {
                "ServiceName": "AsistOff.MES",
                "ServiceVersion": "2.1.0",
                "OtlpEndpoint": "http://otel-collector:4317",
                "SamplingRatio": 0.25,
                "PrometheusEnabled": true
              }
            }
            """);

        // Act
        var options = configuration.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>()!;

        // Assert
        options.ServiceName.Should().Be("AsistOff.MES");
        options.ServiceVersion.Should().Be("2.1.0");
        options.OtlpEndpoint.Should().Be("http://otel-collector:4317");
        options.HasOtlpEndpoint.Should().BeTrue();
        options.GetSamplingRatio().Should().BeApproximately(0.25, precision: 0.0001);
        options.PrometheusEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.5, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1, 1)]
    [InlineData(2.5, 1)]
    public void GetSamplingRatio_OutOfRangeValues_AreClamped(double configured, double expected)
    {
        // Arrange
        var options = new ObservabilityOptions { SamplingRatio = configured };

        // Act
        var actual = options.GetSamplingRatio();

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void GetSamplingRatio_NaN_IsTreatedAsZero()
    {
        // Arrange
        var options = new ObservabilityOptions { SamplingRatio = double.NaN };

        // Act
        var actual = options.GetSamplingRatio();

        // Assert
        actual.Should().Be(0);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("not-a-uri", false)]
    [InlineData("http://otel-collector:4317", true)]
    public void HasOtlpEndpoint_RequiresWellFormedAbsoluteUri(string endpoint, bool expected)
    {
        // Arrange
        var options = new ObservabilityOptions { OtlpEndpoint = endpoint };

        // Act + Assert
        options.HasOtlpEndpoint.Should().Be(expected);
    }

    private static IConfiguration BuildConfiguration(string json) =>
        new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();
}
