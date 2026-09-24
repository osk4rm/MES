using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.TelemetryReadings;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Trend;

/// <summary>
/// Last-N readings of one tag in ascending time order (oldest first) for
/// dashboard sparklines. Paging-free capped list: <see cref="Take"/> 1..200.
/// </summary>
public class BrowseTelemetryTrendRequest : ITenantRequest<IReadOnlyList<TelemetryReadingResponse>>
{
    public Guid TagId { get; set; }

    public int Take { get; set; } = 50;
}
