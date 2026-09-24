using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.TelemetryReadings.Browse;

public class BrowseTelemetryReadingsRequest
    : ITenantRequest<PagedResponse<TelemetryReadingResponse>>, IPagedRequest
{
    public Guid? TagId { get; set; }
    public Guid? MachineId { get; set; }
    public DateTime? ReadAtFrom { get; set; }
    public DateTime? ReadAtTo { get; set; }

    /// <summary>When true, returns only the latest reading per tag.</summary>
    public bool LatestOnly { get; set; }

    public List<string> RawSort { get; set; } = ["ReadAt,desc"];
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["ReadAt"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
