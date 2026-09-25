namespace AsistOff.MES.Shared.Infrastructure.Observability;

/// <summary>
/// Declarative observability settings bound from the <c>Observability</c>
/// configuration section. Every value can be overridden per environment via
/// <c>appsettings.{Environment}.json</c> or environment variables using the
/// <c>Observability__</c> prefix (e.g.
/// <c>Observability__OtlpEndpoint=http://otel-collector:4317</c>).
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// OpenTelemetry resource <c>service.name</c>. Defaults to the product name.
    /// </summary>
    public string ServiceName { get; set; } = "AsistOff.MES";

    /// <summary>
    /// OpenTelemetry resource <c>service.version</c>.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// OTLP/gRPC collector endpoint (e.g. <c>http://otel-collector:4317</c>).
    /// Empty means no collector is configured: tracing and metrics run in
    /// no-op mode (spans are still created for W3C propagation, nothing is
    /// exported) and the application must boot without exceptions.
    /// </summary>
    public string OtlpEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Root sampling probability in the <c>[0, 1]</c> range. Values outside
    /// the range are clamped by <see cref="GetSamplingRatio"/>. Remote-parent
    /// sampling decisions from an upstream W3C <c>traceparent</c> are always
    /// honoured (parent-based sampler).
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// When <c>true</c>, the Prometheus scrape endpoint (<c>/metrics</c>) is
    /// mapped. When <c>false</c> (default) the endpoint is not mapped and
    /// <c>GET /metrics</c> falls through to 404.
    /// </summary>
    public bool PrometheusEnabled { get; set; }

    /// <summary>
    /// Whether an OTLP collector endpoint is configured (and well-formed).
    /// </summary>
    public bool HasOtlpEndpoint => TryGetOtlpUri(out _);

    /// <summary>
    /// Sampling probability clamped to the <c>[0, 1]</c> range.
    /// </summary>
    public double GetSamplingRatio()
    {
        if (double.IsNaN(SamplingRatio))
        {
            return 0;
        }

        return Math.Clamp(SamplingRatio, 0, 1);
    }

    internal bool TryGetOtlpUri(out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(OtlpEndpoint))
        {
            return false;
        }

        return Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out uri);
    }
}
