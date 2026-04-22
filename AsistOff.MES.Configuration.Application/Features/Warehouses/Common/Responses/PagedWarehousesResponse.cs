using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Common.Responses;

public class PagedWarehousesResponse(
    IReadOnlyCollection<WarehouseItemResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<WarehouseItemResponse>(items, totalCount, pageSize);
