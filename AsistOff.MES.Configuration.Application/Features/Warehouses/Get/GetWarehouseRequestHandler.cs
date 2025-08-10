using AsistOff.MES.Configuration.Application.Features.Warehouses.Common;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.Warehouses.Get;

internal sealed class GetWarehouseRequestHandler(
    IWarehousesRepository warehousesRepository)
    : IRequestHandler<GetWarehouseRequest, WarehouseResult>
{
    public async Task<WarehouseResult> Handle(GetWarehouseRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await warehousesRepository.GetByIdAsync(request.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            throw new NotFoundException("Warehouse", request.WarehouseId);
        }

        return new WarehouseResult(warehouse!.Id, warehouse.Name, warehouse.SyncId);
    }
}