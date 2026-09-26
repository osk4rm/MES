using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Contracts.Paging;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.MaterialReservations;

internal sealed class BrowseMaterialReservationsRequestHandler(
    IMaterialReservationsRepository reservationsRepository)
    : IRequestHandler<BrowseMaterialReservationsRequest, PagedResponse<MaterialReservationResponse>>
{
    public async Task<PagedResponse<MaterialReservationResponse>> Handle(
        BrowseMaterialReservationsRequest request, CancellationToken cancellationToken)
    {
        // Tenant isolation comes from the global EF query filter inside the
        // repository; no manual TenantId predicate is written here.
        var predicate = PredicateBuilder.New<MaterialReservation>(true);

        if (request.ProductionOrderId.HasValue)
            predicate = predicate.And(x => x.ProductionOrderId == request.ProductionOrderId.Value);
        if (request.ProductId.HasValue)
            predicate = predicate.And(x => x.ProductId == request.ProductId.Value);
        if (request.WarehouseId.HasValue)
            predicate = predicate.And(x => x.WarehouseId == request.WarehouseId.Value);
        if (request.Status.HasValue)
            predicate = predicate.And(x => x.Status == request.Status.Value);

        var total = await reservationsRepository.CountAsync(predicate, cancellationToken);
        var paginator = new Paginator<MaterialReservation>(predicate, request);
        var items = await reservationsRepository.BrowseAsync(paginator, cancellationToken);

        return new PagedMaterialReservationsResponse(
            items.Select(MaterialReservationResponse.Map).ToList(), total, request.PageSize);
    }
}
