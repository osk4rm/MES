using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.SpcCharacteristics.Responses;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.SpcCharacteristics.Browse;

public class BrowseSpcCharacteristicsRequest
    : ITenantRequest<PagedResponse<SpcCharacteristicResponse>>, IPagedRequest
{
    public string? Search { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? MachineId { get; set; }
    public bool? IsActive { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } =
        ["Code", "Name", "ChartType", "IsActive"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
