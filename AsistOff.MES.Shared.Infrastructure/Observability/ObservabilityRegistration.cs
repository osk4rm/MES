using System.Diagnostics;
using AsistOff.MES.Shared.Abstractions.Observability;
using AsistOff.MES.Shared.Infrastructure.Correlation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Wires the OpenTelemetry SDK: W3C traces (ASP.NET Core hosting, HttpClient,
/// EF Core) exported via OTLP plus ASP.NET Core request metrics and the MES
/// business meters (<see cref="MesMeters"/>) exposed for Prometheus
/// scraping. With no OTLP endpoint configured the SDK runs in
/// no-op mode (spans still propagate W3C context, nothing is exported).
/// </summary>
public static class ObservabilityRegistration
{
    /// <summary>
    /// Registers the OpenTelemetry tracer/metrics providers. Never throws
    /// for missing or malformed observability configuration: an absent OTLP
    /// endpoint simply disables export.
    /// </summary>
    public static IServiceCollection AddMesObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        services.Configure<ObservabilityOptions>(
            configuration.GetSection(ObservabilityOptions.SectionName));

        var resource = BuildResourceBuilder(options);

        var sampler = BuildSampler(options);

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resource)
                .SetSampler(sampler)
                .AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = true;
                    instrumentation.EnrichWithHttpResponse = EnrichResponseWithTenantAndCorrelation;
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporterIfConfigured(options))
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resource)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(MesMeters.MeterName)
                .AddOtlpExporterIfConfigured(options)
                .AddPrometheusExporterIfEnabled(options));

        return services;
    }

    /// <summary>
    /// Builds the OpenTelemetry resource carrying the configured
    /// <c>service.name</c> / <c>service.version</c> identity (internal seam
    /// for tests: asserts the service name exported with every span).
    /// </summary>
    internal static ResourceBuilder BuildResourceBuilder(ObservabilityOptions options) =>
        ResourceBuilder.CreateDefault()
            .AddService(options.ServiceName, serviceVersion: options.ServiceVersion);

    /// <summary>
    /// Builds the root sampler: a parent-based sampler honouring upstream
    /// W3C <c>traceparent</c> decisions, falling back to the configured
    /// sampling ratio (clamped to <c>[0, 1]</c>).
    /// </summary>
    internal static ParentBasedSampler BuildSampler(ObservabilityOptions options) =>
        new(new TraceIdRatioBasedSampler(options.GetSamplingRatio()));

    /// <summary>
    /// Maps the Prometheus scrape endpoint (<c>/metrics</c>) when
    /// <c>Observability:PrometheusEnabled</c> is <c>true</c>. When disabled
    /// nothing is mapped and <c>GET /metrics</c> falls through to 404.
    /// </summary>
    public static IApplicationBuilder UseMesObservability(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices
            .GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        if (options.PrometheusEnabled)
        {
            app.UseOpenTelemetryPrometheusScrapingEndpoint();
        }

        return app;
    }

    private static TracerProviderBuilder AddOtlpExporterIfConfigured(
        this TracerProviderBuilder builder,
        ObservabilityOptions options)
    {
        if (options.TryGetOtlpUri(out var endpoint) && endpoint is not null)
        {
            builder.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
        }

        return builder;
    }

    private static MeterProviderBuilder AddOtlpExporterIfConfigured(
        this MeterProviderBuilder builder,
        ObservabilityOptions options)
    {
        if (options.TryGetOtlpUri(out var endpoint) && endpoint is not null)
        {
            builder.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
        }

        return builder;
    }

    private static MeterProviderBuilder AddPrometheusExporterIfEnabled(
        this MeterProviderBuilder builder,
        ObservabilityOptions options)
    {
        if (options.PrometheusEnabled)
        {
            builder.AddPrometheusExporter();
        }

        return builder;
    }

    /// <summary>
    /// Runs when the server span stops (tenant resolution has completed by
    /// then): re-asserts the correlation tag and adds the ambient
    /// <c>tenant_id</c> tag. Reads tenant state as an opaque tag value only.
    /// Both values come from <c>HttpContext.Items</c>, which survives until
    /// export: <c>HttpContext.RequestServices</c> is already torn down when
    /// this callback fires, so no service resolution is possible here.
    /// </summary>
    private static void EnrichResponseWithTenantAndCorrelation(Activity activity, HttpResponse response)
    {
        var context = response.HttpContext;

        var correlationId = CorrelationIds.GetCurrent(context);
        if (correlationId is not null)
        {
            activity.SetTag(CorrelationIds.ActivityTagKey, correlationId);
        }

        if (TenantTraceEnricher.TryReadTenantId(context, out var tenantId))
        {
            TenantTraceEnricher.TryEnrichWithTenant(activity, tenantId);
        }
    }
}
