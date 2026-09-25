using AsistOff.MES.Shared.Infrastructure.Correlation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Tests.Correlation;

/// <summary>
/// Unit tests for the correlation ID helper (issue #251): verbatim echo of
/// valid incoming GUIDs, fresh GUID generation for absent/invalid input, and
/// current-request resolution.
/// </summary>
public class CorrelationIdsTests
{
    [Fact]
    public void ResolveIncoming_ValidGuid_EchoesVerbatim()
    {
        // Arrange
        var incoming = "3F2504E0-4F89-11D3-9A0C-0305E82C3301";

        // Act
        var result = CorrelationIds.ResolveIncoming(incoming);

        // Assert
        result.Should().Be(incoming);
    }

    [Fact]
    public void ResolveIncoming_Absent_GeneratesValidGuid()
    {
        // Act
        var result = CorrelationIds.ResolveIncoming(null);

        // Assert
        Guid.TryParse(result, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("123")]
    public void ResolveIncoming_InvalidInput_ReplacesWithFreshGuid(string? incoming)
    {
        // Act
        var result = CorrelationIds.ResolveIncoming(incoming);

        // Assert
        result.Should().NotBe(incoming);
        Guid.TryParse(result, out _).Should().BeTrue();
    }

    [Fact]
    public void GetCurrent_NoItem_FallsBackToGuidTraceIdentifier()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.TraceIdentifier = Guid.NewGuid().ToString();

        // Act
        var result = CorrelationIds.GetCurrent(context);

        // Assert
        result.Should().Be(context.TraceIdentifier);
    }

    [Fact]
    public void GetCurrent_NonGuidTraceIdentifier_ReturnsNull()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "0HN123456789ABC";

        // Act
        var result = CorrelationIds.GetCurrent(context);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrent_NullContext_ReturnsNull()
    {
        // Act
        var result = CorrelationIds.GetCurrent(null);

        // Assert
        result.Should().BeNull();
    }
}
