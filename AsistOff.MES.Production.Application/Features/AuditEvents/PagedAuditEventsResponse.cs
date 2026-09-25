using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.AuditEvents;

public class PagedAuditEventsResponse(IReadOnlyCollection<AuditEventResponse> items, int totalCount, int? pageSize)
    : PagedResponse<AuditEventResponse>(items, totalCount, pageSize);
