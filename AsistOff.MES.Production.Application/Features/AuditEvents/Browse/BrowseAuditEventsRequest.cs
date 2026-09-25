using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.AuditEvents.Browse;

/// <summary>
/// Tenant-scoped browse of the append-only audit history (slice 2/2, issue
/// #246). Filter by entity name plus entity id to read the history of one
/// Production Order, Machine or Production Confirmation in descending time
/// order. A read: covered by the <c>AuthorizationAllowlist</c>, no permission
/// attribute required.
/// </summary>
public class BrowseAuditEventsRequest
    : ITenantRequest<PagedResponse<AuditEventResponse>>, IPagedRequest
{
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["ChangedAt"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
