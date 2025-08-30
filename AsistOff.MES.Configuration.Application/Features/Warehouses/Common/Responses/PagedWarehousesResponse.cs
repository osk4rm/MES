using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Common.Responses;

public class PagedWarehousesResponse(
    IReadOnlyCollection<WarehouseResponse> items,
    int totalCount,
    int? pageSize)
    : PagedResponse<WarehouseResponse>(items, totalCount, pageSize);
