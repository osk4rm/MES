using AsistOff.MES.Configuration.Application.Features.Warehouses.Common.Responses;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;

public sealed class BrowseWarehousesRequestHandler(
    IWarehousesRepository warehousesRepository,
    ITenantContext tenantContext)
    : IRequestHandler<BrowseWarehousesRequest, PagedResponse<WarehouseItemResponse>>
{
    public async Task<PagedResponse<WarehouseItemResponse>> Handle(BrowseWarehousesRequest request,
        CancellationToken cancellationToken)
    {
        var predicate = PredicateBuilder.New<Warehouse>(true)
            .And(x => x.TenantId == tenantContext.TenantId);

        if (!string.IsNullOrWhiteSpace(request.Name))
            predicate = predicate.And(x => x.Name.Contains(request.Name));

        var totalCount = await warehousesRepository.CountAsync(cancellationToken);
        var paginator = new Paginator<Warehouse>(predicate, request);

        var warehouses = await warehousesRepository.BrowseAsync(paginator, cancellationToken);

        var items = warehouses
            .Select(w => new WarehouseItemResponse(w.Id, w.Name, w.SyncId))
            .ToList();

        return new PagedWarehousesResponse(items, totalCount, request.PageSize);
    }
}