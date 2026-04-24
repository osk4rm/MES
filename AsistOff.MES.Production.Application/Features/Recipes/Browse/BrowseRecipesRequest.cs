using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.Common;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;

namespace AsistOff.MES.Production.Application.Features.Recipes.Browse;

public class BrowseRecipesRequest
    : ITenantRequest<PagedResponse<RecipeResponse>>, IPagedRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public bool? IsActive { get; set; }
    public Guid? PrimaryProductId { get; set; }
    public List<string> RawSort { get; set; } = new();
    public IReadOnlyCollection<string> SupportedSortFields { get; } = ["Code", "Name", "IsActive"];
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public int? MaxPageSize => 100;
}
