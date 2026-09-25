using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Infrastructure.Correlation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Registers the OpenTelemetry SDK for traces and metrics (issue #252):
/// ASP.NET Core hosting, HttpClient, and EF Core instrumentation with W3C
/// traceparent propagation; OTLP export only when
/// <c>Observability:OtlpEndpoint</c> is configured; Prometheus exporter only
/// when <c>Observability:PrometheusEnabled</c> is true. With no OTLP endpoint
/// the SDK still builds (no-op mode) and the application boots normally.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing and metrics. Never throws for missing or
    /// malformed OTLP endpoint configuration — the exporter is simply skipped.
    /// </summary>
    public static IServiceCollection AddMesObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        services.Configure<ObservabilityOptions>(
            configuration.GetSection(ObservabilityOptions.SectionName));

        var serviceName = options.EffectiveServiceName;
        var serviceVersion = options.EffectiveServiceVersion;
        var samplingRatio = options.NormalizedSamplingRatio;
        var otlpEndpoint = ResolveOtlpEndpoint(options.OtlpEndpoint);

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(serviceName, serviceVersion: serviceVersion))
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.RecordException = true;
                    // Enrich at response time (after the correlation middleware
                    // and authentication have run) so both the effective
                    // correlation ID and the ambient tenant id are available.
                    o.EnrichWithHttpResponse = (activity, response) =>
                    {
                        var context = response.HttpContext;
                        var correlationId = CorrelationIds.GetCurrent(context);
                        if (correlationId is not null)
                        {
                            ObservabilityPropagation.AttachCorrelation(activity, correlationId);
                        }

                        var accessor = context.RequestServices.GetService(typeof(ICurrentTenantAccessor))
                            as ICurrentTenantAccessor;
                        ObservabilityPropagation.TryAttachTenant(activity, accessor);
                    };
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .SetSampler(new TraceIdRatioBasedSampler(samplingRatio))
                .ConfigureOtlpExport(otlpEndpoint))
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(serviceName, serviceVersion: serviceVersion))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .ConfigureOtlpExport(otlpEndpoint)
                .ConfigurePrometheusExport(options.PrometheusEnabled));

        return services;
    }

    internal static Uri? ResolveOtlpEndpoint(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        return Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }

    private static TracerProviderBuilder ConfigureOtlpExport(
        this TracerProviderBuilder builder,
        Uri? otlpEndpoint)
    {
        if (otlpEndpoint is not null)
        {
            builder.AddOtlpExporter(o => o.Endpoint = otlpEndpoint);
        }

        return builder;
    }

    private static MeterProviderBuilder ConfigureOtlpExport(
        this MeterProviderBuilder builder,
        Uri? otlpEndpoint)
    {
        if (otlpEndpoint is not null)
        {
            builder.AddOtlpExporter(o => o.Endpoint = otlpEndpoint);
        }

        return builder;
    }

    private static MeterProviderBuilder ConfigurePrometheusExport(
        this MeterProviderBuilder builder,
        bool enabled)
    {
        if (enabled)
        {
            builder.AddPrometheusExporter();
        }

        return builder;
    }
}
