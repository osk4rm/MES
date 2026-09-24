using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.Lots;

public class PagedLotsResponse(
    IReadOnlyCollection<LotResponse> items, int totalCount, int? pageSize)
    : PagedResponse<LotResponse>(items, totalCount, pageSize);
