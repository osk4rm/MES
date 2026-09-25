using System.Diagnostics;
using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Verifies the <c>X-Correlation-ID</c> contract: a valid incoming GUID is
/// kept, anything absent or invalid is replaced with a fresh GUID, and the
/// effective value is attached to the active trace (span tag + baggage).
/// </summary>
public class CorrelationIdHelperTests
{
    [Theory]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301", true)]
    [InlineData("{3f2504e0-4f89-11d3-9a0c-0305e82c3301}", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("not-a-guid", false)]
    [InlineData("trace-id-123", false)]
    public void IsValidCorrelationId_AcceptsOnlyGuids(string? value, bool expected)
    {
        // Arrange + Act
        var actual = CorrelationIdHelper.IsValidCorrelationId(value);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void ResolveCorrelationId_ValidIncomingId_IsKeptVerbatim()
    {
        // Arrange
        var incoming = Guid.NewGuid().ToString();

        // Act
        var actual = CorrelationIdHelper.ResolveCorrelationId(incoming);

        // Assert
        actual.Should().Be(incoming);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")]
    public void ResolveCorrelationId_MissingOrInvalid_GeneratesFreshGuid(string? incoming)
    {
        // Act
        var actual = CorrelationIdHelper.ResolveCorrelationId(incoming);

        // Assert
        actual.Should().NotBe(incoming);
        CorrelationIdHelper.IsValidCorrelationId(actual).Should().BeTrue();
    }

    [Fact]
    public void GenerateCorrelationId_ReturnsUniqueValidGuids()
    {
        // Act
        var first = CorrelationIdHelper.GenerateCorrelationId();
        var second = CorrelationIdHelper.GenerateCorrelationId();

        // Assert
        CorrelationIdHelper.IsValidCorrelationId(first).Should().BeTrue();
        CorrelationIdHelper.IsValidCorrelationId(second).Should().BeTrue();
        first.Should().NotBe(second);
    }

    [Fact]
    public void AttachToTrace_WithActiveActivity_SetsTagAndBaggage()
    {
        // Arrange
        using var activity = new Activity("test-operation").Start();
        var correlationId = Guid.NewGuid().ToString();

        // Act
        CorrelationIdHelper.AttachToTrace(correlationId);

        // Assert
        activity.GetTagItem(CorrelationIdHelper.ActivityTagKey).Should().Be(correlationId);
        activity.GetBaggageItem(CorrelationIdHelper.BaggageKey).Should().Be(correlationId);
        Baggage.Current.GetBaggage(CorrelationIdHelper.BaggageKey).Should().Be(correlationId);
    }

    [Fact]
    public void AttachToTrace_WithoutActiveActivity_DoesNotThrow_AndSetsBaggage()
    {
        // Arrange
        Activity.Current.Should().BeNull();
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var act = () => CorrelationIdHelper.AttachToTrace(correlationId);

        // Assert
        act.Should().NotThrow();
        Baggage.Current.GetBaggage(CorrelationIdHelper.BaggageKey).Should().Be(correlationId);
    }

    [Fact]
    public void GetEffectiveCorrelationId_MiddlewareHasRun_ReturnsStoredId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var correlationId = Guid.NewGuid().ToString();
        context.Items[CorrelationIdHelper.ItemKey] = correlationId;

        // Act
        var actual = CorrelationIdHelper.GetEffectiveCorrelationId(context);

        // Assert
        actual.Should().Be(correlationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData(42)]
    public void GetEffectiveCorrelationId_MissingOrInvalid_ReturnsNull(object? stored)
    {
        // Arrange
        var context = new DefaultHttpContext();
        if (stored is not null)
        {
            context.Items[CorrelationIdHelper.ItemKey] = stored;
        }

        // Act
        var actual = CorrelationIdHelper.GetEffectiveCorrelationId(context);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public void GetEffectiveCorrelationId_NullContext_ReturnsNull()
    {
        // Act
        var actual = CorrelationIdHelper.GetEffectiveCorrelationId(null);

        // Assert
        actual.Should().BeNull();
    }
}
