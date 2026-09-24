using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.ScrapEvents;

public class PagedScrapEventsResponse(
    IReadOnlyCollection<ScrapEventResponse> items, int totalCount, int? pageSize)
    : PagedResponse<ScrapEventResponse>(items, totalCount, pageSize);
