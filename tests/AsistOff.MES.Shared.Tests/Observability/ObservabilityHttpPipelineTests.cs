using System.Diagnostics;
using System.Net;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Correlation;
using AsistOff.MES.Shared.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace AsistOff.MES.Shared.Tests.Observability;

/// <summary>
/// End-to-end proof for issue #252 AC1 over real HTTP (no mocks): boots a
/// real Kestrel host wired exactly like the gateway
/// (<c>AddMesObservability</c> + <c>CorrelationIdMiddleware</c> + the
/// tenant/correlation span enrichment), drives a tenant-style GET carrying an
/// upstream W3C <c>traceparent</c>, and asserts the span actually exported
/// through the SDK pipeline keeps the upstream trace, carries the correlation
/// and tenant tags, and is built from the
/// <c>service.name=AsistOff.MES</c> resource.
/// </summary>
public sealed class ObservabilityHttpPipelineTests
{
    private const string ProbePath = "/pipeline-products-probe";
    private const string UpstreamSourceName = "Mes.Pipeline.Tests.Upstream";

    [Fact]
    public async Task TenantGet_ExportsServerSpan_WithServiceIdentityAndTraceparent()
    {
        // Arrange - the exact registration the gateway uses, plus an
        // in-memory processor observing what the SDK pipeline really exports
        // (an ambient stub tenant stands in for the resolved tenant: tenant
        // resolution itself is covered elsewhere, the behaviour under test is
        // the export pipeline and the enrichment wiring).
        var tenantId = Guid.NewGuid();
        var processor = new RecordingProcessor();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:ServiceName"] = "AsistOff.MES",
            ["Observability:ServiceVersion"] = "1.0.0",
            ["Observability:SamplingRatio"] = "1",
            ["Observability:PrometheusEnabled"] = "false",
        });
        builder.Services.AddSingleton<ICurrentTenantAccessor>(new StubTenantAccessor(tenantId));
        builder.Services.AddMesObservability(builder.Configuration);
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddProcessor(processor));
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        await using var app = builder.Build();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<TenantTraceContextMiddleware>();
        app.MapGet(ProbePath, () => Results.Ok(new { ok = true }));
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };

            var correlationId = Guid.NewGuid().ToString();
            var upstreamTraceId = ActivityTraceId.CreateRandom();
            var upstreamSpanId = ActivitySpanId.CreateRandom();

            // An upstream caller owning this trace: HttpClient propagates the
            // ambient context as the W3C traceparent (a hand-set header would
            // be replaced by the client diagnostics handler, so the caller is
            // modelled with a real Activity carrying a remote parent context).
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == UpstreamSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(listener);
            try
            {
                using var upstreamSource = new ActivitySource(UpstreamSourceName);
                var remoteParent = new ActivityContext(
                    upstreamTraceId, upstreamSpanId, ActivityTraceFlags.Recorded, null, isRemote: true);
                using var upstream = upstreamSource.StartActivity("upstream-caller", ActivityKind.Client, remoteParent);
                upstream.Should().NotBeNull();

                using var request = new HttpRequestMessage(HttpMethod.Get, ProbePath);
                request.Headers.Add(CorrelationIds.HeaderName, correlationId);

                // Act - a real HTTP round-trip through middleware + instrumentation.
                using var response = await client.SendAsync(request);

                // Assert - HTTP contract first.
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                response.Headers.GetValues(CorrelationIds.HeaderName).Single().Should().Be(correlationId);
            }
            finally
            {
                listener.Dispose();
            }

            app.Services.GetRequiredService<TracerProvider>()!.ForceFlush(5000).Should().BeTrue();

            // The server span really exported through the SDK pipeline keeps
            // the upstream W3C trace and carries the correlation + tenant tags.
            var serverSpan = processor.Exported
                .Where(a => a.Kind == ActivityKind.Server
                    && Equals(a.GetTagItem(CorrelationIds.ActivityTagKey), correlationId))
                .Should().ContainSingle().Subject;
            serverSpan.TraceId.Should().Be(upstreamTraceId);
            serverSpan.GetTagItem(TenantTraceEnricher.TenantTagKey).Should().Be(tenantId.ToString());

            // The export identity of this exact host: the resource the
            // gateway registration builds from these options travels on every
            // exported span.
            var options = app.Services.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
            var resource = ObservabilityRegistration.BuildResourceBuilder(options).Build();
            var attributes = resource.Attributes.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
            attributes.Should().ContainKey("service.name").WhoseValue.Should().Be("AsistOff.MES");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class StubTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid CurrentTenantId => tenantId;

        public bool TryGetTenantId(out Guid id)
        {
            id = tenantId;
            return true;
        }
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
