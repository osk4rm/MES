namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Configuration for OpenTelemetry traces and metrics (issue #252).
/// Bound from the <c>Observability</c> configuration section; every setting
/// supports environment variable overrides via the standard <c>__</c>
/// separator (e.g. <c>Observability__OtlpEndpoint</c>,
/// <c>Observability__PrometheusEnabled</c>).
/// No tenant-scoped state; no <c>ISaasy</c>, no query-filter bypass.
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// OTLP collector endpoint (e.g. <c>http://otel-collector:4317</c>).
    /// Empty or whitespace means no exporter is registered and tracing runs
    /// in no-op mode; the application still boots and serves traffic.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>Logical service name reported on every span/metric. Default <c>AsistOff.MES</c>.</summary>
    public string ServiceName { get; set; } = "AsistOff.MES";

    /// <summary>Service version reported as resource attribute. Default <c>1.0.0</c>.</summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Head-based sampling ratio, 0..1. Values outside the range are clamped
    /// by <see cref="NormalizedSamplingRatio"/>. Default 1.0 (sample all).
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// When <c>true</c>, the Prometheus scrape endpoint (<c>/metrics</c>) is
    /// mapped. When <c>false</c> (default), <c>/metrics</c> falls through to
    /// the normal 404 pipeline.
    /// </summary>
    public bool PrometheusEnabled { get; set; }

    /// <summary>True when an OTLP endpoint is configured (non-empty).</summary>
    public bool HasOtlpEndpoint => !string.IsNullOrWhiteSpace(OtlpEndpoint);

    /// <summary>
    /// Sampling ratio clamped to 0..1 and with NaN/Infinity coerced to the
    /// safe defaults (NaN -&gt; 1.0, +Infinity -&gt; 1.0, -Infinity -&gt; 0.0).
    /// </summary>
    public double NormalizedSamplingRatio
    {
        get
        {
            if (double.IsNaN(SamplingRatio))
            {
                return 1.0;
            }

            if (double.IsPositiveInfinity(SamplingRatio))
            {
                return 1.0;
            }

            if (double.IsNegativeInfinity(SamplingRatio))
            {
                return 0.0;
            }

            return Math.Clamp(SamplingRatio, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Effective service name, falling back to <c>AsistOff.MES</c> when blank.
    /// </summary>
    public string EffectiveServiceName =>
        string.IsNullOrWhiteSpace(ServiceName) ? "AsistOff.MES" : ServiceName.Trim();

    /// <summary>
    /// Effective service version, falling back to <c>1.0.0</c> when blank.
    /// </summary>
    public string EffectiveServiceVersion =>
        string.IsNullOrWhiteSpace(ServiceVersion) ? "1.0.0" : ServiceVersion.Trim();
}
