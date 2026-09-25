using System.Diagnostics;
using AsistOff.MES.Shared.Infrastructure.Correlation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AsistOff.MES.Shared.Tests.Correlation;

/// <summary>
/// Verifies the issue #252 trace bridge on the canonical correlation
/// middleware: the effective <c>X-Correlation-ID</c> is attached to the
/// active W3C trace (span tag plus baggage for downstream propagation)
/// without disturbing an upstream <c>traceparent</c>, and the middleware
/// stays null-safe when no <see cref="Activity"/> is active.
/// </summary>
public sealed class CorrelationTracePropagationTests : IDisposable
{
    private const string SourceName = "Mes.TracePropagation.Tests";

    private readonly ActivityListener _listener = new()
    {
        ShouldListenTo = source => source.Name == SourceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };

    private readonly ActivitySource _source = new(SourceName);

    public CorrelationTracePropagationTests()
    {
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    [Fact]
    public async Task Invoke_WithActiveW3CActivity_PreservesTrace_AndAttachesCorrelation()
    {
        // Arrange - simulate the server Activity the ASP.NET Core hosting
        // instrumentation starts from an upstream W3C traceparent.
        var parentTraceId = ActivityTraceId.CreateRandom();
        var parentContext = new ActivityContext(
            parentTraceId,
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);
        using var activity = _source.StartActivity("GET /health/live", ActivityKind.Server, parentContext);
        activity.Should().NotBeNull();

        var incoming = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIds.HeaderName] = incoming;

        // Capture the ambient baggage where downstream handlers and
        // HttpClient calls would observe it (AsyncLocal flows downstream,
        // not back to the caller, so it must be read inside next).
        string? baggageInNext = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            baggageInNext = Baggage.Current.GetBaggage(CorrelationIds.BaggageKey);
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert - the upstream trace is untouched while the effective
        // correlation id rides the span tag plus W3C baggage.
        Activity.Current.Should().BeSameAs(activity);
        activity!.TraceId.Should().Be(parentTraceId);
        activity.GetTagItem(CorrelationIds.ActivityTagKey).Should().Be(incoming);
        activity.GetBaggageItem(CorrelationIds.BaggageKey).Should().Be(incoming);
        baggageInNext.Should().Be(incoming);
    }

    [Fact]
    public async Task Invoke_WithoutIncomingHeader_AttachesGeneratedIdToTrace()
    {
        // Arrange
        using var activity = _source.StartActivity("GET /health/live", ActivityKind.Server);
        activity.Should().NotBeNull();

        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - the generated id links the span, the baggage and the
        // echoed response header on a single identifier.
        var effective = context.Response.Headers[CorrelationIds.HeaderName].Single();
        activity!.GetTagItem(CorrelationIds.ActivityTagKey).Should().Be(effective);
        activity.GetBaggageItem(CorrelationIds.BaggageKey).Should().Be(effective);
    }

    [Fact]
    public async Task Invoke_WithoutActiveActivity_AttachesBaggageWithoutThrowing()
    {
        // Arrange - no ambient Activity (e.g. instrumentation disabled).
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            var incoming = Guid.NewGuid().ToString();
            var context = new DefaultHttpContext();
            context.Request.Headers[CorrelationIds.HeaderName] = incoming;

            string? baggageInNext = null;
            var middleware = new CorrelationIdMiddleware(_ =>
            {
                baggageInNext = Baggage.Current.GetBaggage(CorrelationIds.BaggageKey);
                return Task.CompletedTask;
            });

            // Act
            var act = () => middleware.InvokeAsync(context);

            // Assert
            await act.Should().NotThrowAsync();
            baggageInNext.Should().Be(incoming);
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Fact]
    public async Task Invoke_PushesEffectiveId_IntoSerilogLogContext()
    {
        // Arrange - issue #252 AC2: server logs must carry the same effective
        // correlation id that rides the trace. Capture Serilog events written
        // inside the downstream pipeline; FromLogContext must surface it.
        var sink = new CapturingSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        try
        {
            var incoming = Guid.NewGuid().ToString();
            var context = new DefaultHttpContext();
            context.Request.Headers[CorrelationIds.HeaderName] = incoming;
            var middleware = new CorrelationIdMiddleware(_ =>
            {
                Log.Information("inside-request");
                return Task.CompletedTask;
            });

            // Act
            await middleware.InvokeAsync(context);

            // Assert - the log event carries the effective (echoed) id.
            sink.Events.Should().ContainSingle();
            sink.Events[0].Properties.TryGetValue(CorrelationIds.LogPropertyName, out var value).Should().BeTrue();
            value?.ToString().Trim('"').Should().Be(incoming);
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
