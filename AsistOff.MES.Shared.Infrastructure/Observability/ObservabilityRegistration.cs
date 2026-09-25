using System.Diagnostics;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
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
/// EF Core) exported via OTLP plus ASP.NET Core request metrics exposed for
/// Prometheus scraping. With no OTLP endpoint configured the SDK runs in
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

        var resource = ResourceBuilder.CreateDefault()
            .AddService(options.ServiceName, serviceVersion: options.ServiceVersion);

        var sampler = new ParentBasedSampler(
            new TraceIdRatioBasedSampler(options.GetSamplingRatio()));

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
                .AddOtlpExporterIfConfigured(options)
                .AddPrometheusExporterIfEnabled(options));

        return services;
    }

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
    /// </summary>
    private static void EnrichResponseWithTenantAndCorrelation(Activity activity, HttpResponse response)
    {
        var context = response.HttpContext;

        var correlationId = CorrelationIdHelper.GetEffectiveCorrelationId(context);
        if (correlationId is not null)
        {
            activity.SetTag(CorrelationIdHelper.ActivityTagKey, correlationId);
        }

        var accessor = context.RequestServices.GetService<ICurrentTenantAccessor>();
        if (accessor is not null && accessor.TryGetTenantId(out var tenantId))
        {
            TenantTraceEnricher.TryEnrichWithTenant(activity, tenantId);
        }
    }
}
