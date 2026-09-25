using AsistOff.MES.Shared.Infrastructure.Correlation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AsistOff.MES.Shared.Tests.Correlation;

/// <summary>
/// Unit tests for the correlation middleware (issue #251): GUID generation
/// when absent, verbatim echo of a valid incoming ID, replacement of invalid
/// input, TraceIdentifier assignment, response echo, and LogContext push.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Invoke_NoIncomingHeader_GeneratesGuid_AndEchoesOnResponse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        string? seenInNext = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            seenInNext = CorrelationIds.GetCurrent(context);
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        seenInNext.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(seenInNext!, out _).Should().BeTrue();
        context.TraceIdentifier.Should().Be(seenInNext);
        context.Items[CorrelationIds.ItemKey].Should().Be(seenInNext);
        context.Response.Headers[CorrelationIds.HeaderName].Should().ContainSingle()
            .Which.Should().Be(seenInNext);
    }

    [Fact]
    public async Task Invoke_ValidIncomingId_EchoesVerbatim()
    {
        // Arrange
        var incoming = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIds.HeaderName] = incoming;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.TraceIdentifier.Should().Be(incoming);
        context.Items[CorrelationIds.ItemKey].Should().Be(incoming);
        context.Response.Headers[CorrelationIds.HeaderName].Should().ContainSingle()
            .Which.Should().Be(incoming);
    }

    [Fact]
    public async Task Invoke_InvalidIncomingId_ReplacesWithFreshGuid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIds.HeaderName] = "not-a-guid";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var effective = context.TraceIdentifier;
        effective.Should().NotBe("not-a-guid");
        Guid.TryParse(effective, out _).Should().BeTrue();
        context.Response.Headers[CorrelationIds.HeaderName].Should().ContainSingle()
            .Which.Should().Be(effective);
    }

    [Fact]
    public async Task Invoke_PushesCorrelationId_IntoSerilogLogContext()
    {
        // Arrange - capture Serilog events written inside the downstream
        // pipeline; FromLogContext must surface the pushed CorrelationId.
        var sink = new CapturingSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        try
        {
            var context = new DefaultHttpContext();
            var incoming = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationIds.HeaderName] = incoming;
            string? loggedCorrelation = null;
            var middleware = new CorrelationIdMiddleware(_ =>
            {
                Log.Information("inside-request");
                return Task.CompletedTask;
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            sink.Events.Should().ContainSingle();
            sink.Events[0].Properties.TryGetValue(CorrelationIds.LogPropertyName, out var value).Should().BeTrue();
            loggedCorrelation = value?.ToString().Trim('"');
            loggedCorrelation.Should().Be(incoming);
        }
        finally
        {
            Log.CloseAndFlush();
            Log.Logger = previous;
        }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
