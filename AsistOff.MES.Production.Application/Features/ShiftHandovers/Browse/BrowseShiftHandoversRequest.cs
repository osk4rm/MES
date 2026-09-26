using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.ShiftHandovers.Browse;

/// <summary>
/// Paged shift handover logbook, newest boundary first. Filters narrow the
/// read to one Work Center and to entries whose boundary start falls inside
/// <c>[From, To]</c>.
/// </summary>
public class BrowseShiftHandoversRequest
    : ITenantRequest<PagedShiftHandoversResponse>, IPagedRequest
{
    public Guid? MachineId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<string> RawSort { get; set; } = ["From,desc"];
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["From", "MachineId", "CreatedAt"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 20;
    public int? MaxPageSize => 100;
}
