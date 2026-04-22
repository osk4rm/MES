using AsistOff.MES.Configuration.Application.Features.ProductGroups.Common.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.ProductGroups.Browse;

internal sealed class BrowseProductGroupsRequestHandler(
    IProductGroupsRepository productGroupsRepository,
    ITenantContext tenantContext)
    : IRequestHandler<BrowseProductGroupsRequest, PagedResponse<ProductGroupResponse>>
{
    public async Task<PagedResponse<ProductGroupResponse>> Handle(BrowseProductGroupsRequest request,
        CancellationToken cancellationToken)
    {
        var filter = BuildPredicate(request);
        var totalCount = await productGroupsRepository.CountAsync(cancellationToken);
        var paginator = new Paginator<ProductGroup>(filter, request);

        var productGroups = await productGroupsRepository.BrowseAsync(paginator, cancellationToken);

        var items = productGroups.Select(x => new ProductGroupResponse(
            x.Id,
            x.SyncId,
            x.Code,
            x.Name,
            x.Description,
            x.IsActive,
            x.ParentGroupId.HasValue ? new ParentGroupResponse(x.ParentGroupId.Value, x.ParentGroup!.Code) : null))
            .ToList();

        return new PagedProductGroupsResponse(items, totalCount, request.PageSize);
    }

    private ExpressionStarter<ProductGroup> BuildPredicate(BrowseProductGroupsRequest request)
    {
        var predicate = PredicateBuilder.New<ProductGroup>(true);
        predicate = predicate.And(x => x.TenantId == tenantContext.TenantId);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            predicate = predicate
                .And(x => x.Name.Contains(request.Name));
        }

        if (!string.IsNullOrEmpty(request.Code))
        {
            predicate = predicate.And(x => x.Code.Contains(request.Code));
        }

        if (request.IsActive.HasValue)
        {
            predicate = predicate.And(x => x.IsActive == request.IsActive);
        }

        if (request.ParentId.HasValue)
        {
            predicate = predicate.And(x => x.ParentGroupId == request.ParentId);
        }

        return predicate;
    }
}