using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.LotGenealogy;

public class PagedLotGenealogyEdgesResponse(
    IReadOnlyCollection<LotGenealogyEdgeResponse> items, int totalCount, int? pageSize)
    : PagedResponse<LotGenealogyEdgeResponse>(items, totalCount, pageSize);
