using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;

public class PagedProductGroupsResponse(IReadOnlyCollection<ProductGroupResponse> items, int totalCount, int? pageSize)
    : PagedResponse<ProductGroupResponse>(items, totalCount, pageSize);