using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.Common;

public class PagedProductionOrdersResponse(IReadOnlyCollection<ProductionOrderResponse> items, int totalCount, int? pageSize)
    : PagedResponse<ProductionOrderResponse>(items, totalCount, pageSize);
