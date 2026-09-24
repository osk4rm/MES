using AsistOff.MES.Multitenancy.Contracts.Interfaces;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Export;

/// <summary>
/// CSV export of the reading log honoring the same filters as the browse
/// endpoint. Returns the formatted CSV document (at most 5000 data rows).
/// Served via content negotiation on <c>GET /api/telemetry-readings</c>
/// with <c>Accept: text/csv</c>.
/// </summary>
public class ExportTelemetryReadingsRequest : ITenantRequest<string>
{
    public Guid? TagId { get; set; }
    public Guid? MachineId { get; set; }
    public DateTime? ReadAtFrom { get; set; }
    public DateTime? ReadAtTo { get; set; }

    /// <summary>When true, exports only the latest reading per tag.</summary>
    public bool LatestOnly { get; set; }
}
