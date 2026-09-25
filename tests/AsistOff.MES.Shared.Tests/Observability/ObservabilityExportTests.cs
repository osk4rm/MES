using System.Diagnostics;
using AsistOff.MES.Shared.Infrastructure.Correlation;
using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// Proves the export half of issue #252 AC1 with the real OpenTelemetry SDK
/// pipeline (no mocks): a server span started under the SDK provider built
/// from <c>BuildResourceBuilder</c>/<c>BuildSampler</c> is captured by an
/// in-memory processor carrying the <c>service.name=AsistOff.MES</c> resource
/// identity, the correlation tag, and the upstream W3C <c>traceparent</c>.
/// </summary>
public sealed class ObservabilityExportTests
{
    [Fact]
    public void ExportedServerSpan_CarriesServiceIdentity_AndCorrelationTag()
    {
        // Arrange - the exact resource + sampler the gateway registers.
        var options = new ObservabilityOptions { ServiceName = "AsistOff.MES", ServiceVersion = "1.0.0" };
        var resource = ObservabilityRegistration.BuildResourceBuilder(options).Build();
        var attributes = resource.Attributes.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
        attributes.Should().ContainKey("service.name")
            .WhoseValue.Should().Be("AsistOff.MES");

        var sourceName = "Mes.Export.Tests." + Guid.NewGuid().ToString("N");
        var processor = new RecordingProcessor();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ObservabilityRegistration.BuildResourceBuilder(options))
            .SetSampler(ObservabilityRegistration.BuildSampler(new ObservabilityOptions { SamplingRatio = 1 }))
            .AddSource(sourceName)
            .AddProcessor(processor)
            .Build();
        tracerProvider.Should().NotBeNull();

        using var source = new ActivitySource(sourceName);
        var correlationId = Guid.NewGuid().ToString();

        // Act - simulate the server span the ASP.NET Core hosting
        // instrumentation starts for a tenant GET, then enriched with the
        // effective correlation id (same tag key the middleware uses).
        using (var activity = source.StartActivity("GET /api/products", ActivityKind.Server))
        {
            activity.Should().NotBeNull();
            CorrelationIds.AttachToTrace(correlationId);
            activity!.Stop();
        }

        tracerProvider!.ForceFlush(5000).Should().BeTrue();

        // Assert - the span was actually exported through the SDK pipeline
        // (not just configured) with the correlation tag and a valid W3C id.
        var exported = processor.Exported.Should().ContainSingle().Subject;
        exported.GetTagItem(CorrelationIds.ActivityTagKey).Should().Be(correlationId);
        exported.TraceId.ToString().Should().MatchRegex("^[0-9a-f]{32}$");
        exported.SpanId.ToString().Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void ExportedServerSpan_PreservesUpstreamW3CTraceparent()
    {
        // Arrange - an upstream W3C traceparent as sent by a caller.
        var upstreamTraceId = ActivityTraceId.CreateRandom().ToString();
        var upstreamSpanId = ActivitySpanId.CreateRandom().ToString();
        var traceparent = $"00-{upstreamTraceId}-{upstreamSpanId}-01";
        ActivityContext.TryParse(traceparent, null, out var parentContext).Should().BeTrue();
        parentContext.TraceId.ToString().Should().Be(upstreamTraceId);

        var sourceName = "Mes.Export.Tests." + Guid.NewGuid().ToString("N");
        var processor = new RecordingProcessor();

        // Even with a zero root ratio the parent-based sampler must honour
        // the upstream sampled decision (issue #252 AC1).
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ObservabilityRegistration.BuildResourceBuilder(new ObservabilityOptions()))
            .SetSampler(ObservabilityRegistration.BuildSampler(new ObservabilityOptions { SamplingRatio = 0 }))
            .AddSource(sourceName)
            .AddProcessor(processor)
            .Build();

        using var source = new ActivitySource(sourceName);
        var correlationId = Guid.NewGuid().ToString();

        // Act
        using (var activity = source.StartActivity("GET /api/products", ActivityKind.Server, parentContext))
        {
            activity.Should().NotBeNull();
            CorrelationIds.AttachToTrace(correlationId);
            activity!.Stop();
        }

        tracerProvider!.ForceFlush(5000).Should().BeTrue();

        // Assert - the child keeps the upstream trace id (W3C propagation)
        // while carrying the correlation tag for log/trace joining.
        var exported = processor.Exported.Should().ContainSingle().Subject;
        exported.TraceId.ToString().Should().Be(upstreamTraceId);
        exported.GetTagItem(CorrelationIds.ActivityTagKey).Should().Be(correlationId);

        // And the context re-injects to a traceparent header for the same trace.
        var carrier = new Dictionary<string, string>();
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(exported.Context, Baggage.Current),
            carrier,
            (c, k, v) => c[k] = v);
        carrier.Should().ContainKey("traceparent");
        carrier["traceparent"].Should().StartWith($"00-{upstreamTraceId}-");
    }

    private sealed class RecordingProcessor : BaseProcessor<Activity>
    {
        private readonly object _lock = new();
        private readonly List<Activity> _exported = new();

        public IReadOnlyList<Activity> Exported
        {
            get
            {
                lock (_lock)
                {
                    return _exported.ToList();
                }
            }
        }

        public override void OnEnd(Activity data)
        {
            lock (_lock)
            {
                _exported.Add(data);
            }
        }
    }
}
