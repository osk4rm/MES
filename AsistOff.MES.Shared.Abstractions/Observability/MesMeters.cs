using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;

namespace AsistOff.MES.Shared.Abstractions.Observability;

/// <summary>
/// MES business meters on the <c>AsistOff.MES</c> meter: shopfloor throughput
/// (confirmations), quality loss (scrap events), availability loss (downtime
/// events) and analytics latency (OEE snapshot and confirmation durations) as
/// first-class Prometheus series with trace-linked exemplars.
///
/// <para>PromQL series and label set (all series carry <c>tenant_id</c> read
/// from the ambient tenant context as a tag only — meters never cross tenant
/// boundaries):</para>
/// <list type="table">
/// <item><term><c>mes_confirmations_total</c> (counter)</term><description>Operator confirmations per Production Order. Labels: <c>work_center_id</c>, <c>tenant_id</c>.</description></item>
/// <item><term><c>mes_scrap_total</c> (counter)</term><description>Scrap events. Labels: <c>work_center_id</c>, <c>reason_code</c> (controlled-vocabulary code, <c>unknown</c> when unresolvable), <c>tenant_id</c>.</description></item>
/// <item><term><c>mes_downtime_events_total</c> (counter)</term><description>Downtime events. Labels: <c>work_center_id</c>, <c>reason_code</c>, <c>tenant_id</c>.</description></item>
/// <item><term><c>mes_oee_snapshot_duration_seconds</c> (histogram)</term><description>Per-Work Center OEE snapshot query latency. Labels: <c>work_center_id</c>, <c>tenant_id</c>. Use <c>mes_oee_snapshot_duration_seconds_count</c> / <c>_sum</c> / <c>_bucket</c> in PromQL.</description></item>
/// <item><term><c>mes_confirmation_duration_seconds</c> (histogram)</term><description>Confirmation handler latency. Labels: <c>work_center_id</c>, <c>tenant_id</c>.</description></item>
/// </list>
///
/// <para>Cardinality contract: label values are bounded by construction.
/// <c>work_center_id</c> and <c>tenant_id</c> are entity/tenant Guids (one
/// value per Work Center / tenant). <c>reason_code</c> accepts only
/// controlled-vocabulary codes (letters, digits, <c>_</c>, <c>-</c>,
/// <c>.</c>, max 64 chars); anything else — lot codes, serials, operator
/// names, tokens — is rejected to the <c>other</c> placeholder and
/// null/empty resolves to <c>unknown</c>. Hashing is deliberately NOT used:
/// a hash still yields one distinct value per distinct input, so it hides
/// raw values without bounding cardinality. Never add raw lot, serial,
/// operator or token values as labels.</para>
///
/// <para>Exemplars: measurements are recorded synchronously inside the
/// handling request scope, so the OpenTelemetry SDK links each sample to the
/// active server span (<c>Activity.Current</c>) and the Prometheus exporter
/// emits it as a trace exemplar. Recording with no active span or no
/// listener is a silent no-op.</para>
/// </summary>
public static partial class MesMeters
{
    /// <summary>Meter name subscribed by the Gateway OpenTelemetry pipeline.</summary>
    public const string MeterName = "AsistOff.MES";

    /// <summary>Operator confirmations counter (<c>mes_confirmations_total</c>).</summary>
    public const string ConfirmationsCounterName = "mes_confirmations_total";

    /// <summary>Scrap events counter (<c>mes_scrap_total</c>).</summary>
    public const string ScrapCounterName = "mes_scrap_total";

    /// <summary>Downtime events counter (<c>mes_downtime_events_total</c>).</summary>
    public const string DowntimeCounterName = "mes_downtime_events_total";

    /// <summary>OEE snapshot latency histogram (<c>mes_oee_snapshot_duration_seconds</c>).</summary>
    public const string OeeSnapshotHistogramName = "mes_oee_snapshot_duration_seconds";

    /// <summary>Confirmation latency histogram (<c>mes_confirmation_duration_seconds</c>).</summary>
    public const string ConfirmationHistogramName = "mes_confirmation_duration_seconds";

    /// <summary>Handling Work Center (machine) label key.</summary>
    public const string WorkCenterLabel = "work_center_id";

    /// <summary>Controlled-vocabulary reason label key.</summary>
    public const string ReasonCodeLabel = "reason_code";

    /// <summary>Ambient tenant label key (tag only, never a cross-tenant read).</summary>
    public const string TenantLabel = "tenant_id";

    /// <summary>Placeholder when no reason code resolves (deleted or cross-tenant id).</summary>
    public const string UnknownReasonCode = "unknown";

    /// <summary>Bounded placeholder for values rejected by the cardinality guard.</summary>
    public const string OtherReasonCode = "other";

    private const int MaxReasonCodeLength = 64;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.\\-]*$")]
    private static partial Regex ReasonCodePattern();

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> ConfirmationsCounter =
        Meter.CreateCounter<long>(ConfirmationsCounterName, unit: "{confirmation}", description: "Operator confirmations per Production Order.");

    private static readonly Counter<long> ScrapCounter =
        Meter.CreateCounter<long>(ScrapCounterName, unit: "{event}", description: "Scrap events with reason code.");

    private static readonly Counter<long> DowntimeCounter =
        Meter.CreateCounter<long>(DowntimeCounterName, unit: "{event}", description: "Downtime events with reason code.");

    private static readonly Histogram<double> OeeSnapshotHistogram =
        Meter.CreateHistogram<double>(OeeSnapshotHistogramName, unit: "s", description: "Per-Work Center OEE snapshot query duration.");

    private static readonly Histogram<double> ConfirmationHistogram =
        Meter.CreateHistogram<double>(ConfirmationHistogramName, unit: "s", description: "Confirmation handler duration.");

    /// <summary>
    /// Records one operator confirmation plus its handling latency. Call only
    /// on the success path — rejected confirmations are not throughput.
    /// </summary>
    public static void RecordConfirmation(Guid workCenterId, Guid tenantId, TimeSpan duration)
    {
        var tags = new TagList
        {
            { WorkCenterLabel, FormatId(workCenterId) },
            { TenantLabel, FormatTenant(tenantId) }
        };
        ConfirmationsCounter.Add(1, tags);
        ConfirmationHistogram.Record(duration.TotalSeconds, tags);
    }

    /// <summary>
    /// Records one scrap event. <paramref name="reasonCode"/> should be the
    /// controlled-vocabulary <c>ReasonCode.Code</c>; unresolvable or
    /// high-cardinality values collapse to <c>unknown</c> / <c>other</c>.
    /// Call only on the success path.
    /// </summary>
    public static void RecordScrap(Guid workCenterId, string? reasonCode, Guid tenantId)
    {
        ScrapCounter.Add(1, new TagList
        {
            { WorkCenterLabel, FormatId(workCenterId) },
            { ReasonCodeLabel, NormalizeReasonCode(reasonCode) },
            { TenantLabel, FormatTenant(tenantId) }
        });
    }

    /// <summary>
    /// Records one downtime event. Same reason-code contract as
    /// <see cref="RecordScrap"/>. Call only on the success path.
    /// </summary>
    public static void RecordDowntime(Guid workCenterId, string? reasonCode, Guid tenantId)
    {
        DowntimeCounter.Add(1, new TagList
        {
            { WorkCenterLabel, FormatId(workCenterId) },
            { ReasonCodeLabel, NormalizeReasonCode(reasonCode) },
            { TenantLabel, FormatTenant(tenantId) }
        });
    }

    /// <summary>
    /// Records one per-Work Center OEE snapshot query latency. Call only on
    /// the success path — failed reads (400/404) are not analytics latency.
    /// </summary>
    public static void RecordOeeSnapshot(Guid workCenterId, Guid tenantId, TimeSpan duration)
    {
        OeeSnapshotHistogram.Record(duration.TotalSeconds, new TagList
        {
            { WorkCenterLabel, FormatId(workCenterId) },
            { TenantLabel, FormatTenant(tenantId) }
        });
    }

    /// <summary>
    /// Bounds the <c>reason_code</c> label: controlled-vocabulary codes pass
    /// through, null/empty becomes <c>unknown</c>, and anything longer than
    /// 64 chars or outside <c>[A-Za-z0-9_.-]</c> (lot codes, serials,
    /// operator names, tokens) collapses to the single <c>other</c> value.
    /// </summary>
    public static string NormalizeReasonCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return UnknownReasonCode;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxReasonCodeLength || !ReasonCodePattern().IsMatch(trimmed))
        {
            return OtherReasonCode;
        }

        return trimmed;
    }

    private static string FormatId(Guid value) => value.ToString("D");

    private static string FormatTenant(Guid tenantId) =>
        tenantId == Guid.Empty ? UnknownReasonCode : tenantId.ToString("D");
}
