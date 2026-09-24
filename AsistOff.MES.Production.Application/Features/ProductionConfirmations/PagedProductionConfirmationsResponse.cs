using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.ProductionConfirmations;

public class PagedProductionConfirmationsResponse(
    IReadOnlyCollection<ProductionConfirmationResponse> items, int totalCount, int? pageSize)
    : PagedResponse<ProductionConfirmationResponse>(items, totalCount, pageSize);
