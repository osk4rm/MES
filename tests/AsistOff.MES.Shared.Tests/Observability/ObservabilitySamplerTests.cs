using System.Diagnostics;
using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using OpenTelemetry.Trace;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Verifies sampler behaviour (issue #252): the parent-based sampler honours
/// an upstream W3C <c>traceparent</c> decision even when the configured root
/// ratio would drop new traces, while new root traces obey the ratio.
/// </summary>
public sealed class ObservabilitySamplerTests
{
    [Fact]
    public void BuildSampler_RemoteSampledParent_IsHonoured_EvenWithZeroRatio()
    {
        // Arrange - ratio 0 drops new roots, but an upstream traceparent
        // that is already sampled must stay sampled.
        var sampler = ObservabilityRegistration.BuildSampler(new ObservabilityOptions { SamplingRatio = 0 });
        var remoteParent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            isRemote: true);

        // Act
        var result = sampler.ShouldSample(new SamplingParameters(
            remoteParent,
            ActivityTraceId.CreateRandom(),
            "upstream-sampled",
            ActivityKind.Server));

        // Assert
        result.Decision.Should().Be(SamplingDecision.RecordAndSample);
    }

    [Fact]
    public void BuildSampler_RootTrace_ObeysConfiguredZeroRatio()
    {
        // Arrange
        var sampler = ObservabilityRegistration.BuildSampler(new ObservabilityOptions { SamplingRatio = 0 });

        // Act
        var result = sampler.ShouldSample(new SamplingParameters(
            default,
            ActivityTraceId.CreateRandom(),
            "new-root",
            ActivityKind.Server));

        // Assert
        result.Decision.Should().NotBe(SamplingDecision.RecordAndSample);
    }
}
