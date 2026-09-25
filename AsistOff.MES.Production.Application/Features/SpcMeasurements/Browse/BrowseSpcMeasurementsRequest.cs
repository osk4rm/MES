using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.SpcMeasurements.Browse;

public class BrowseSpcMeasurementsRequest
    : ITenantRequest<PagedResponse<SpcMeasurementResponse>>, IPagedRequest
{
    public Guid? CharacteristicId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<string> RawSort { get; set; } = ["MeasuredAt,asc"];
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["MeasuredAt", "Value"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
