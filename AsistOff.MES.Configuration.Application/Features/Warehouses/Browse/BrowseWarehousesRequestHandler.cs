using System.Collections.Immutable;
using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Configuration.Domain.Repositories;
using MediatR;
using ErrorOr;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Browse;

public sealed class BrowseWarehousesRequestHandler(
    IWarehousesRepository warehousesRepository)
    : IRequestHandler<BrowseWarehousesRequest, ErrorOr<IReadOnlyCollection<WarehouseResult>>>
{
    public async Task<ErrorOr<IReadOnlyCollection<WarehouseResult>>> Handle(BrowseWarehousesRequest request,
        CancellationToken cancellationToken)
    {
        var warehouses = await warehousesRepository.BrowseAsync(cancellationToken);

        return warehouses.Select(x => new WarehouseResult(x.Id, x.Name, x.SyncId)).ToImmutableList();
    }
}