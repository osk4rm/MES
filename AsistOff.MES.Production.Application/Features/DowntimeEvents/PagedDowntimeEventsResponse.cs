using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.DowntimeEvents;

public class PagedDowntimeEventsResponse(
    IReadOnlyCollection<DowntimeEventResponse> items, int totalCount, int? pageSize)
    : PagedResponse<DowntimeEventResponse>(items, totalCount, pageSize);
