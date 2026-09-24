using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Domain.Enums;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.AndonSignals.Browse;

public class BrowseAndonSignalsRequest
    : ITenantRequest<PagedResponse<AndonSignalResponse>>, IPagedRequest
{
    public Guid? MachineId { get; set; }
    public AndonSignalCategory? Category { get; set; }
    public AndonSignalStatus? Status { get; set; }
    public DateTime? RaisedFrom { get; set; }
    public DateTime? RaisedTo { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["RaisedAt", "MachineId", "Category", "Status"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 50;
    public int? MaxPageSize => 200;
}
