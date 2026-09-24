using AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Responses;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.OperatorShiftAssignments.Browse;

public class BrowseOperatorShiftAssignmentsRequest
    : ITenantRequest<PagedResponse<OperatorShiftAssignmentResponse>>, IPagedRequest
{
    public DateOnly? Date { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public Guid? OperatorId { get; set; }
    public Guid? ShiftId { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } =
        ["Date", "OperatorId", "ShiftId"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
