using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.Common;

public class PagedRecipesResponse(IReadOnlyCollection<RecipeResponse> items, int totalCount, int? pageSize)
    : PagedResponse<RecipeResponse>(items, totalCount, pageSize);
