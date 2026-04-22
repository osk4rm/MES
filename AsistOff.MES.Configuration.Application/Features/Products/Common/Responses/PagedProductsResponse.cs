using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.Products.Common.Responses;

public class PagedProductsResponse(IReadOnlyCollection<ProductResponse> items, int totalCount, int? pageSize)
    : PagedResponse<ProductResponse>(items, totalCount, pageSize);