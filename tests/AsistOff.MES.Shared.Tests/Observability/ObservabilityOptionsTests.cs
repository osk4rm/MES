using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Unit tests for the observability options binding (issue #252): OTLP
/// endpoint presence, sampling ratio bounds, Prometheus flag, and
/// service name/version fallbacks.
/// </summary>
public class ObservabilityOptionsTests
{
    [Fact]
    public void Bind_EmptySection_YieldsSafeDefaults()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        // Act
        var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
            ?? new ObservabilityOptions();

        // Assert
        options.OtlpEndpoint.Should().BeNull();
        options.HasOtlpEndpoint.Should().BeFalse();
        options.EffectiveServiceName.Should().Be("AsistOff.MES");
        options.EffectiveServiceVersion.Should().Be("1.0.0");
        options.NormalizedSamplingRatio.Should().Be(1.0);
        options.PrometheusEnabled.Should().BeFalse();
    }

    [Fact]
    public void Bind_FullSection_ReadsAllValues()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:OtlpEndpoint"] = "http://otel-collector:4317",
            ["Observability:ServiceName"] = "AsistOff.MES",
            ["Observability:ServiceVersion"] = "2.1.0",
            ["Observability:SamplingRatio"] = "0.25",
            ["Observability:PrometheusEnabled"] = "true",
        }).Build();

        // Act
        var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()!;

        // Assert
        options.HasOtlpEndpoint.Should().BeTrue();
        options.EffectiveServiceName.Should().Be("AsistOff.MES");
        options.EffectiveServiceVersion.Should().Be("2.1.0");
        options.NormalizedSamplingRatio.Should().BeApproximately(0.25, 1e-9);
        options.PrometheusEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HasOtlpEndpoint_BlankValue_IsFalse(string endpoint)
    {
        // Arrange
        var options = new ObservabilityOptions { OtlpEndpoint = endpoint };

        // Act
        var result = options.HasOtlpEndpoint;

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.25, 0.25)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 1.0)]
    public void NormalizedSamplingRatio_ClampsToBounds(double configured, double expected)
    {
        // Arrange
        var options = new ObservabilityOptions { SamplingRatio = configured };

        // Act
        var result = options.NormalizedSamplingRatio;

        // Assert
        result.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void NormalizedSamplingRatio_NaN_FallsBackToOne()
    {
        // Arrange
        var options = new ObservabilityOptions { SamplingRatio = double.NaN };

        // Act
        var result = options.NormalizedSamplingRatio;

        // Assert
        result.Should().Be(1.0);
    }

    [Fact]
    public void NormalizedSamplingRatio_PositiveInfinity_FallsBackToOne()
    {
        // Arrange
        var options = new ObservabilityOptions { SamplingRatio = double.PositiveInfinity };

        // Act
        var result = options.NormalizedSamplingRatio;

        // Assert
        result.Should().Be(1.0);
    }

    [Fact]
    public void NormalizedSamplingRatio_NegativeInfinity_FallsBackToZero()
    {
        // Arrange
        var options = new ObservabilityOptions { SamplingRatio = double.NegativeInfinity };

        // Act
        var result = options.NormalizedSamplingRatio;

        // Assert
        result.Should().Be(0.0);
    }

    [Theory]
    [InlineData(null, "AsistOff.MES")]
    [InlineData("", "AsistOff.MES")]
    [InlineData("   ", "AsistOff.MES")]
    [InlineData(" Custom.Svc ", "Custom.Svc")]
    public void EffectiveServiceName_FallsBackWhenBlank(string? configured, string expected)
    {
        // Arrange
        var options = new ObservabilityOptions { ServiceName = configured! };

        // Act
        var result = options.EffectiveServiceName;

        // Assert
        result.Should().Be(expected);
    }
}
