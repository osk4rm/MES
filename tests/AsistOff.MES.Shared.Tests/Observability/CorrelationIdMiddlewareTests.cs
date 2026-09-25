using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Verifies the correlation-id middleware contract: the effective id is
/// stored for error envelopes, echoed on the response, kept verbatim when
/// valid and regenerated when absent or invalid.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Invoke_WithoutHeader_StoresAndEchoesFreshGuid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var stored = context.Items[CorrelationIdHelper.ItemKey].Should().BeOfType<string>().Subject;
        CorrelationIdHelper.IsValidCorrelationId(stored).Should().BeTrue();
        context.Response.Headers[CorrelationIdHelper.HeaderName].Should().ContainSingle()
            .Which.Should().Be(stored);
    }

    [Fact]
    public async Task Invoke_ValidIncomingId_IsEchoedVerbatim()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var incoming = Guid.NewGuid().ToString();
        context.Request.Headers[CorrelationIdHelper.HeaderName] = incoming;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items[CorrelationIdHelper.ItemKey].Should().Be(incoming);
        context.Response.Headers[CorrelationIdHelper.HeaderName].Should().ContainSingle()
            .Which.Should().Be(incoming);
    }

    [Fact]
    public async Task Invoke_InvalidIncomingId_IsReplacedWithFreshGuid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdHelper.HeaderName] = "not-a-guid";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var stored = context.Items[CorrelationIdHelper.ItemKey].Should().BeOfType<string>().Subject;
        stored.Should().NotBe("not-a-guid");
        CorrelationIdHelper.IsValidCorrelationId(stored).Should().BeTrue();
        context.Response.Headers[CorrelationIdHelper.HeaderName].Should().ContainSingle()
            .Which.Should().Be(stored);
    }
}
